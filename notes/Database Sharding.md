# 데이터베이스 샤딩 (Database Sharding)

---

## 1. 개념

**샤딩**이란 하나의 대용량 데이터베이스를 **여러 개의 작은 조각(Shard)으로 분산 저장**하는 수평 파티셔닝(Horizontal Partitioning) 기법입니다. 각 샤드는 독립된 DB 서버에 저장되며, 전체 데이터의 일부분만 담당합니다.

---

## 2. 왜 필요한가?

| 문제 상황                      | 샤딩으로 해결             |
| ------------------------------ | ------------------------- |
| 단일 DB 서버의 용량 한계       | 여러 서버에 데이터 분산   |
| 트래픽 집중으로 인한 성능 저하 | 부하를 여러 서버에 분산   |
| 수직 확장(Scale-Up)의 한계     | 수평 확장(Scale-Out) 가능 |

### 실제 수치로 보기

PostgreSQL 단일 서버의 현실적 한계를 넘어서는 순간 샤딩을 고민하게 됩니다.

```
데이터 크기:   수 TB 이상 → 인덱스가 메모리에 안 올라오기 시작
동시 커넥션:   1,000개 초과 → context switching 비용 급증
QPS:           수만 초과 → 단순 쿼리도 큐잉 발생
디스크 I/O:    IOPS 한계 도달 → NVMe SSD도 버티지 못함
```

### 도입 순서 (샤딩은 최후의 수단)

```
캐싱 도입 (Redis)
    ↓
쿼리 최적화 + 인덱스 튜닝
    ↓
읽기 전용 복제본 (Read Replica)
    ↓
수직 확장 (Scale-Up)
    ↓
🔴 샤딩 (복잡도가 크게 증가)
```

---

## 3. 샤딩 전략

### 3-1. 범위 기반 샤딩 (Range-based)

특정 컬럼 값의 **범위**로 분할합니다.

```
users 테이블 (user_id 기준)

Shard A: user_id  1 ~ 1,000,000
Shard B: user_id  1,000,001 ~ 2,000,000
Shard C: user_id  2,000,001 ~ 3,000,000
```

**예시: 날짜 기반 로그 테이블**

```sql
-- Shard 2023: logs WHERE created_at BETWEEN '2023-01-01' AND '2023-12-31'
-- Shard 2024: logs WHERE created_at BETWEEN '2024-01-01' AND '2024-12-31'
-- Shard 2025: logs WHERE created_at BETWEEN '2025-01-01' AND '2025-12-31'

-- 최근 로그 조회 → Shard 2025만 접근 (효율적)
SELECT * FROM logs WHERE created_at > '2025-06-01';
```

장점: 구현 단순, 범위 쿼리 효율적  
단점: 신규 데이터가 마지막 샤드에 집중 (Hotspot), 샤드 간 데이터 불균형

### 3-2. 해시 기반 샤딩 (Hash-based)

`shard_index = hash(shard_key) % shard_count` 공식으로 분배합니다.

```
user_id = 12345  →  hash(12345) % 4 = 1  →  Shard 1
user_id = 67890  →  hash(67890) % 4 = 2  →  Shard 2
user_id = 11111  →  hash(11111) % 4 = 3  →  Shard 3
```

**예시: 애플리케이션 레벨 라우팅**

```python
import hashlib

SHARD_COUNT = 4

def get_shard(user_id: int) -> str:
    shard_index = user_id % SHARD_COUNT
    return f"db_shard_{shard_index}"

# user_id=1001 → db_shard_1
# user_id=1002 → db_shard_2
conn = get_connection(get_shard(user_id))
conn.execute("SELECT * FROM users WHERE user_id = ?", [user_id])
```

장점: 균등 분산, Hotspot 방지  
단점: 샤드 수 변경 시 **대규모 재분배** 발생 (아래 문제 참고)

**⚠️ 샤드 수 변경 문제**

```
기존: hash(key) % 4  →  shard 0,1,2,3
변경: hash(key) % 5  →  shard 0,1,2,3,4

user_id=100: 100 % 4 = 0  →  100 % 5 = 0  (그대로)
user_id=101: 101 % 4 = 1  →  101 % 5 = 1  (그대로)
user_id=102: 102 % 4 = 2  →  102 % 5 = 2  (그대로)
user_id=103: 103 % 4 = 3  →  103 % 5 = 3  (그대로)
user_id=104: 104 % 4 = 0  →  104 % 5 = 4  ← 이동 필요!
user_id=108: 108 % 4 = 0  →  108 % 5 = 3  ← 이동 필요!

→ 전체 데이터의 약 80%가 다른 샤드로 이동해야 함
```

### 3-3. 일관된 해싱 (Consistent Hashing)

해시 링(Hash Ring)을 사용해 샤드 수 변경 시 **최소한의 데이터만 이동**시킵니다.

```
        0
       /  \
   300     60
   |          |
  240        120
       \  /
        180

각 샤드가 링 위의 특정 지점을 담당:
Shard A: 0   ~ 90
Shard B: 90  ~ 180
Shard C: 180 ~ 270
Shard D: 270 ~ 360(0)

key의 hash값이 100이면 → Shard B 담당
key의 hash값이 200이면 → Shard C 담당
```

**샤드 추가 시 영향 범위**

```
Shard E를 150 지점에 추가

변경 전: hash 90~180 → 모두 Shard B
변경 후: hash 90~150 → Shard B
         hash 150~180 → Shard E  ← 이 범위만 이동!

→ 전체 데이터의 1/N만 이동 (N = 전체 샤드 수)
```

**가상 노드(Virtual Node)로 균등성 향상**

물리 샤드 1개가 링 위의 여러 지점을 담당하게 해서 데이터 불균형을 해소합니다.

```
Shard A → 실제로는 링 위 A1, A2, A3, A4 지점 담당
Shard B → 링 위 B1, B2, B3, B4 지점 담당
(가상 노드가 많을수록 균등 분포)
```

장점: 동적 확장에 유리, 최소 데이터 이동  
단점: 구현 복잡, 가상 노드 수 튜닝 필요  
사용처: Cassandra, DynamoDB, Redis Cluster

### 3-4. 디렉토리 기반 샤딩 (Directory-based)

**조회 테이블(Lookup Table)**에 키 → 샤드 매핑을 저장합니다.

```
[Lookup Table (별도 서버에 저장)]

user_id  | shard
---------|-------
1001     | shard_2
1002     | shard_1
1003     | shard_3
...

→ 조회: "user_id=1001은 어디?" → shard_2 → shard_2에서 실제 데이터 조회
```

장점: 가장 유연함 (특정 유저를 다른 샤드로 수동 이동 가능)  
단점: 조회 테이블 자체가 병목 · 단일 장애점(SPOF), 추가 네트워크 왕복 발생

---

## 4. 샤드 키(Shard Key) 설계 — 가장 중요한 결정

잘못된 샤드 키는 샤딩 전체를 무의미하게 만듭니다.

### 좋은 샤드 키의 조건

| 조건            | 설명                               | 나쁜 예                   |
| --------------- | ---------------------------------- | ------------------------- |
| 높은 카디널리티 | 다양한 값이 존재해야 고르게 분산   | 성별(M/F) — 2가지뿐       |
| 균등한 분포     | 특정 값에 집중 없어야 함           | 국가 코드 — KR에 90% 집중 |
| 쿼리 패턴 부합  | 자주 쓰는 쿼리가 단일 샤드 내 해결 | 게시글을 태그로 샤딩      |
| 불변성          | 자주 변경되지 않는 값              | 이메일 주소               |

### 실제 설계 예시

**SNS 서비스 — 게시글 테이블**

```sql
-- ❌ 나쁜 선택: created_at 기준 샤딩
-- 문제: 최신 게시글 조회가 항상 마지막 샤드에 집중 (Hotspot)

-- ❌ 나쁜 선택: 카테고리 기준 샤딩
-- 문제: "일상" 카테고리에 데이터 몰림, 카디널리티 낮음

-- ✅ 좋은 선택: user_id 기준 샤딩
-- 이유: 높은 카디널리티, 균등 분포, "내 게시글 조회"가 단일 샤드로 해결

-- 단, 팔로잉 피드(여러 user_id 조회)는 cross-shard 발생 → 별도 처리 필요
```

**이커머스 — 주문 테이블**

```sql
-- ✅ 옵션 1: user_id 기준
-- 장점: "내 주문 내역 조회" 단일 샤드
-- 단점: 특정 VIP 고객의 주문이 많으면 불균형

-- ✅ 옵션 2: order_id 기준 (해시)
-- 장점: 완전 균등 분산
-- 단점: "특정 고객 주문 전체 조회" 시 cross-shard 발생

-- → 서비스 특성에 따라 선택
```

---

## 5. 샤딩의 주요 문제와 해결책

### 5-1. Cross-Shard Join

여러 샤드에 걸친 JOIN이 불가능합니다.

```sql
-- user_id 기준으로 샤딩된 경우
-- users 테이블: user_id 1~100만 → Shard A
-- orders 테이블: user_id 기준 동일하게 샤딩됨

-- ✅ 같은 샤드 키라면 같은 샤드에 있으므로 JOIN 가능
-- (user_id=5000의 users, orders 둘 다 Shard A에 있음)

-- ❌ 문제: 친구 관계 (user_id=5000, friend_id=900000)
-- user 5000 → Shard A
-- user 900000 → Shard C
-- 두 유저 정보 JOIN 불가
```

**해결책**

```python
# 애플리케이션 레벨에서 합치기
user_a = shard_a.query("SELECT * FROM users WHERE user_id = 5000")
user_b = shard_c.query("SELECT * FROM users WHERE user_id = 900000")

# 애플리케이션에서 직접 JOIN 로직 구현
result = merge(user_a, user_b)

# → DB 레벨 JOIN보다 느리고 코드 복잡도 증가
```

**데이터 비정규화로 사전 해결**

```sql
-- JOIN이 필요 없도록 필요한 데이터를 미리 복사
-- orders 테이블에 user_name, user_email을 비정규화로 저장

CREATE TABLE orders (
    order_id    BIGINT,
    user_id     BIGINT,
    user_name   VARCHAR(100),  -- ← users에서 복사 (비정규화)
    user_email  VARCHAR(200),  -- ← users에서 복사 (비정규화)
    total_price DECIMAL,
    ...
);
-- 단점: 유저 정보 변경 시 orders 테이블도 업데이트 필요
```

### 5-2. 분산 트랜잭션

두 개 이상의 샤드에 걸친 트랜잭션 보장이 어렵습니다.

```
계좌 이체 예시:
user_id=100 (Shard A)의 잔액 -10,000원
user_id=200 (Shard C)의 잔액 +10,000원

→ Shard A는 성공, Shard C에서 실패하면?
→ 기존의 단일 DB ACID 트랜잭션으로 해결 불가
```

**해결책 1 — 2PC (Two-Phase Commit)**

```
Phase 1 (Prepare): 코디네이터가 모든 샤드에 "준비됐어?" 물어봄
    Shard A: "준비됨 (PREPARED)"
    Shard C: "준비됨 (PREPARED)"

Phase 2 (Commit): 모두 준비됐으면 "커밋해!" 명령
    Shard A: COMMIT
    Shard C: COMMIT

만약 하나라도 ABORT면 → 전체 ROLLBACK

단점: 코디네이터 장애 시 모든 샤드가 잠금 상태로 대기
```

**해결책 2 — SAGA 패턴 (MSA에서 주로 사용)**

```
각 단계를 독립 트랜잭션으로 분리하고,
실패 시 보상 트랜잭션(Compensating Transaction)으로 롤백

1. Shard A: 잔액 -10,000원 (성공)
2. Shard C: 잔액 +10,000원 (실패!)
3. 보상 트랜잭션: Shard A 잔액 +10,000원 복구

장점: 분산 락 없음, 높은 가용성
단점: 일시적 불일치 허용, 보상 로직 구현 복잡
```

### 5-3. Hotspot (데이터/트래픽 불균형)

특정 샤드에만 트래픽이 집중되는 현상입니다.

```
예시: 유명인의 SNS 게시글
  일반 유저 A (user_id=1001) → 하루 조회 100건
  연예인 B  (user_id=5000) → 하루 조회 1억 건

→ user_id 기준 샤딩 시 연예인 B가 있는 샤드만 과부하
```

**해결책 — 핫 키 특별 처리**

```python
HOT_USERS = {5000, 10023, 77777}  # 유명인 목록

def get_shard(user_id: int) -> list[str]:
    if user_id in HOT_USERS:
        # 핫 유저는 여러 샤드에 복제 후 읽기 분산
        return [f"shard_{user_id % SHARD_COUNT}_replica_{i}" for i in range(3)]
    return [f"shard_{user_id % SHARD_COUNT}"]
```

### 5-4. Re-sharding (샤드 재분배)

데이터가 늘어나 샤드를 추가할 때 대규모 데이터 이동이 발생합니다.

```
기존: 4개 샤드, 각 250GB → 총 1TB
증설: 8개 샤드로 확장

hash 기반이라면:
→ 기존 4개 샤드의 데이터 50%를 새 4개 샤드로 이동
→ 약 500GB 데이터 마이그레이션 필요
→ 이 과정 중 서비스 중단 또는 성능 저하 발생
```

**해결책 — 온라인 마이그레이션 전략**

```
1단계: 새 샤드를 추가하되 쓰기는 기존 샤드에만
2단계: 이중 쓰기 (기존 + 새 샤드 동시에)
3단계: 백그라운드로 기존 데이터 복사
4단계: 동기화 완료 후 새 샤드로 트래픽 전환
5단계: 기존 샤드에서 해당 데이터 삭제

→ 서비스 중단 없이 점진적 마이그레이션 가능
```

---

## 6. 샤딩 아키텍처 패턴

### 6-1. 단순 샤딩 (Application-Level)

```
[Application]
     |
     |-- shard_key % N 계산
     |
  ┌──┴──┐
Shard0  Shard1  Shard2  ...

→ 앱이 직접 샤드 선택
→ 단순하지만 앱 코드에 샤딩 로직 침투
```

### 6-2. 프록시 기반 샤딩

```
[Application]
     ↓
[Sharding Proxy]  ← SQL 파싱 후 자동 라우팅
  ┌──┼──┐
Shard0 Shard1 Shard2

예: PgDog (PostgreSQL), Vitess (MySQL), ProxySQL
→ 앱은 단일 DB에 연결하는 것처럼 코드 작성
```

### 6-3. 데이터베이스 내장 샤딩

```
[Application]
     ↓
[MongoDB / Cassandra / CockroachDB]
     ↓
  자동으로 내부 샤딩 처리

→ 개발자가 샤딩을 신경 쓸 필요 없음
→ 단, 해당 DB에 종속됨
```

---

## 7. 샤딩 vs 파티셔닝

| 구분     | 파티셔닝            | 샤딩                      |
| -------- | ------------------- | ------------------------- |
| 위치     | 단일 DB 인스턴스 내 | 여러 DB 인스턴스 분산     |
| 목적     | 쿼리 성능 향상      | 수평 확장, 부하 분산      |
| 네트워크 | 불필요              | 인스턴스 간 네트워크 필요 |
| 관리     | 비교적 단순         | 복잡                      |
| 트랜잭션 | ACID 보장           | 제한적                    |

**파티셔닝 예시 (PostgreSQL)**

```sql
-- 같은 DB 안에서 테이블을 날짜별로 나눔
CREATE TABLE orders (
    order_id    BIGINT,
    created_at  DATE,
    ...
) PARTITION BY RANGE (created_at);

CREATE TABLE orders_2024 PARTITION OF orders
    FOR VALUES FROM ('2024-01-01') TO ('2025-01-01');

CREATE TABLE orders_2025 PARTITION OF orders
    FOR VALUES FROM ('2025-01-01') TO ('2026-01-01');

-- 앱에서는 그냥 orders 조회, DB가 알아서 파티션 선택
SELECT * FROM orders WHERE created_at > '2025-06-01';
-- → 내부적으로 orders_2025만 스캔
```

---

## 8. 실제 사례

| 서비스    | 방식                  | 샤드 키              |
| --------- | --------------------- | -------------------- |
| Instagram | PostgreSQL 샤딩       | user_id              |
| Discord   | Cassandra (내장 샤딩) | 서버(길드) ID        |
| Airbnb    | MySQL + Vitess        | listing_id / user_id |
| YouTube   | MySQL + Vitess        | video_id             |
| MongoDB   | 내장 샤딩 클러스터    | 커스텀 샤드 키       |
| DynamoDB  | 일관된 해싱 자동 분산 | 파티션 키            |

**Instagram 사례 상세**

초기에 단일 PostgreSQL 서버로 시작했다가 사용자 폭증으로 샤딩을 도입했습니다.

- user_id 기반 해시 샤딩 사용
- 게시글, 팔로우, 좋아요 등 모두 user_id 기준으로 같은 샤드에 배치
- "한 유저의 모든 데이터는 같은 샤드에" 원칙 → 대부분 쿼리가 cross-shard 불필요

---

## 9. 핵심 요약

```
샤딩 도입 체크리스트

□ 정말 샤딩이 필요한가? (캐싱, Read Replica로 충분하지 않은가?)
□ 샤드 키를 신중하게 선택했는가?
□ 주요 쿼리 패턴이 단일 샤드 내에서 해결되는가?
□ Cross-shard 쿼리 처리 방법을 설계했는가?
□ 분산 트랜잭션 전략을 수립했는가?
□ Re-sharding 계획을 세웠는가?
□ 모니터링 체계를 갖췄는가?
```

> 💡 **핵심 원칙**: 샤딩은 강력하지만 복잡성이 크게 증가합니다. 좋은 샤드 키 설계가 전부라고 해도 과언이 아닙니다. 처음부터 샤딩을 고려한 설계(샤드 키 선택, 쿼리 패턴 파악)가 나중의 마이그레이션 비용을 크게 줄여줍니다.
