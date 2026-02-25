### **Apache Cassandra**

---

#### **개요**

Apache Cassandra는 Facebook이 개발하고 현재 Apache Software Foundation에서 관리하는 오픈소스 분산 NoSQL 데이터베이스입니다. 단일 장애점(SPOF) 없이 여러 노드에 데이터를 분산 저장하도록 설계되어, 높은 가용성과 수평 확장성이 요구되는 대규모 환경에 적합합니다. Netflix, Instagram, Discord 등에서 수백 TB 이상의 데이터를 처리하는 데 사용하고 있습니다.

---

#### **핵심 설계 철학**

Cassandra는 CAP 이론에서 **AP(Availability + Partition Tolerance)** 를 선택한 데이터베이스입니다. 즉, 네트워크 장애가 발생해도 서비스는 계속 동작하지만, 일시적인 데이터 불일치(최종 일관성, Eventual Consistency)를 허용합니다.

```
CAP 이론
  C (Consistency)   — 모든 노드가 항상 같은 데이터를 반환
  A (Availability)  — 항상 응답을 반환
  P (Partition Tolerance) — 네트워크 분리 상황에서도 동작

Cassandra → A + P 선택
  "일단 응답은 항상 한다. 대신 잠깐 오래된 데이터를 줄 수 있다."
```

---

#### **아키텍처**

**마스터 없는 링 구조 (Masterless Ring)**

모든 노드가 동등한 역할을 합니다. Primary/Replica 개념이 없고, 어느 노드에 요청해도 됩니다.

```
        Node A
       /       \
  Node D       Node B
       \       /
        Node C

- 모든 노드가 동등 (Master 없음)
- 클라이언트는 아무 노드에나 연결 가능
- 노드 추가/제거 시 자동으로 데이터 재분배
```

**데이터 분산 — 일관된 해싱(Consistent Hashing)**

각 노드가 해시 링의 특정 범위를 담당합니다. 새 노드를 추가하면 인접 노드의 데이터 일부만 이동합니다.

**복제 (Replication)**

데이터를 여러 노드에 자동으로 복제합니다.

```
Replication Factor = 3 이면
→ 동일 데이터를 3개 노드에 복제
→ 2개 노드가 동시에 죽어도 서비스 가능
```

---

#### **데이터 모델**

관계형 DB와 겉모습은 비슷하지만 설계 철학이 완전히 다릅니다.

| 개념         | RDBMS       | Cassandra                      |
| ------------ | ----------- | ------------------------------ |
| 데이터 단위  | Row         | Row                            |
| 묶음 단위    | Table       | Table                          |
| 데이터베이스 | Database    | Keyspace                       |
| 고유 식별자  | Primary Key | Partition Key + Clustering Key |

**Partition Key와 Clustering Key**

```sql
CREATE TABLE messages (
    room_id    UUID,       -- Partition Key: 어느 노드에 저장할지 결정
    sent_at    TIMESTAMP,  -- Clustering Key: 파티션 내 정렬 기준
    user_id    UUID,
    content    TEXT,
    PRIMARY KEY (room_id, sent_at)
);

-- room_id가 같은 데이터는 같은 노드에 저장됨
-- sent_at 기준으로 정렬되어 저장됨
-- → "특정 채팅방의 최근 메시지 조회"가 매우 빠름
```

**⚠️ 쿼리 우선 설계 (Query-First Design)**

Cassandra는 JOIN이 없고, WHERE 조건도 Partition Key 기준으로만 효율적으로 동작합니다. 따라서 **"어떤 쿼리를 쓸 것인가"를 먼저 결정한 후 테이블을 설계**해야 합니다. RDBMS처럼 정규화보다 비정규화가 일반적입니다.

---

#### **일관성 수준 (Consistency Level)**

읽기/쓰기 시 몇 개의 노드에서 응답을 받아야 성공으로 처리할지 요청마다 조절할 수 있습니다.

| 레벨           | 설명                   | 특징                  |
| -------------- | ---------------------- | --------------------- |
| `ONE`          | 1개 노드 응답          | 빠름, 일관성 낮음     |
| `QUORUM`       | 과반수 노드 응답       | 균형 (가장 많이 사용) |
| `ALL`          | 전체 노드 응답         | 강한 일관성, 느림     |
| `LOCAL_QUORUM` | 같은 데이터센터 과반수 | 멀티 DC 환경에서 사용 |

```
Replication Factor=3 일 때 QUORUM = 2개 노드 응답 필요

쓰기 QUORUM + 읽기 QUORUM
→ 반드시 최신 데이터를 읽을 수 있음 (Strong Consistency 달성 가능)
```

---

#### **주요 데이터 타입**

- `UUID`, `TIMEUUID`: 고유 식별자. `TIMEUUID`는 시간 정보가 포함되어 정렬에 활용됩니다.
- `TEXT`, `VARCHAR`: 문자열 데이터.
- `INT`, `BIGINT`, `VARINT`: 정수형 데이터.
- `DECIMAL`, `FLOAT`, `DOUBLE`: 소수점 데이터.
- `TIMESTAMP`, `DATE`, `TIME`: 날짜 및 시간 데이터.
- `BOOLEAN`: 참/거짓 값.
- `LIST`, `SET`, `MAP`: 컬렉션 타입. 하나의 컬럼에 여러 값을 저장할 수 있습니다.
- `BLOB`: 바이너리 데이터.

---

#### **쿼리 언어 — CQL (Cassandra Query Language)**

SQL과 문법이 유사하지만 JOIN, 서브쿼리, 집계 함수 등은 지원하지 않습니다.

```sql
-- Keyspace 생성
CREATE KEYSPACE my_app
  WITH replication = {'class': 'SimpleStrategy', 'replication_factor': 3};

-- 테이블 생성
CREATE TABLE my_app.users (
    user_id   UUID PRIMARY KEY,
    name      TEXT,
    email     TEXT,
    created_at TIMESTAMP
);

-- 삽입
INSERT INTO my_app.users (user_id, name, email, created_at)
VALUES (uuid(), 'Kim', 'kim@example.com', toTimestamp(now()));

-- 조회 (Partition Key 기준만 효율적)
SELECT * FROM my_app.users WHERE user_id = ?;
```

---

#### **주요 관리 도구**

- **nodetool**: Cassandra 내장 CLI 도구로 노드 상태 확인, 데이터 복구, 클러스터 관리 등을 수행합니다.
- **DataStax Studio**: Cassandra 전용 GUI 도구로 CQL 쿼리 작성, 데이터 시각화, 스키마 관리를 지원합니다.
- **Apache Cassandra CQLSH**: 기본 내장 CLI 클라이언트로 CQL 쿼리를 직접 실행할 수 있습니다.

---

#### **언제 사용하면 좋은가**

| 적합한 경우               | 부적합한 경우                         |
| ------------------------- | ------------------------------------- |
| 쓰기가 매우 많은 서비스   | JOIN이 많은 복잡한 쿼리               |
| 글로벌 멀티 리전 서비스   | 강한 일관성이 필수인 금융 거래        |
| 시계열 데이터 (로그, IoT) | 데이터 모델이 자주 바뀌는 초기 서비스 |
| 99.999% 가용성 요구       | 소규모 단일 서버 환경                 |
