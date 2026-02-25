# Vitess

## 한 줄 요약

> MySQL을 수평 확장(샤딩)할 수 있게 해주는 **클러스터링 미들웨어**. YouTube가 만들었고, 현재는 CNCF Graduated 프로젝트.

---

## 탄생 배경

2010년대 초 YouTube는 동영상·댓글·조회수 폭증으로 MySQL 단일 서버가 한계에 달했습니다. 수직 확장도 한계였고, 앱 레벨에서 직접 샤딩하면 코드가 너무 복잡해지는 문제가 있었습니다. 그래서 "MySQL 앞에 스마트한 프록시 레이어를 놓자"는 아이디어로 Go 언어로 Vitess를 개발했고, 2019년 Kubernetes, Prometheus와 같은 등급인 CNCF Graduated 프로젝트가 됩니다.

---

## 전체 아키텍처

```
[ Application ]
      ↓ (MySQL 프로토콜 그대로 연결)
[ VTGate ]  ← 진입점 프록시, SQL 파싱 + 샤드 라우팅
      ↓           ↓           ↓
[ VTTablet ] [ VTTablet ] [ VTTablet ]  ← 각 MySQL 앞 사이드카
    MySQL 0     MySQL 1     MySQL 2

[ VTCtld ]  ← 클러스터 메타데이터, 토폴로지 관리 (etcd 사용)
```

---

## 핵심 컴포넌트 3가지

**VTGate** — 앱이 접속하는 단일 엔드포인트입니다. SQL을 파싱해서 어느 샤드로 갈지 결정하고, cross-shard 쿼리면 여러 샤드에 분산 실행 후 결과를 합쳐서 반환합니다. 커넥션 풀링도 여기서 처리합니다.

**VTTablet** — MySQL 프로세스 옆에 붙는 에이전트입니다. 쿼리 제어, 커넥션 풀 관리, 헬스체크, Primary/Replica 구분 라우팅을 담당합니다.

**VTCtld** — 클러스터의 두뇌입니다. "Shard 0의 Primary는 어느 서버인가" 같은 메타데이터를 etcd에 저장하고, 장애 시 Failover 조율을 합니다.

---

## 샤딩 방식

Vitess는 **Keyspace ID** 개념으로 샤드를 결정합니다.

```
shard_key → hash → Keyspace ID (0x00 ~ 0xFF) → 샤드 결정

customer_id=1234 → hash → 0x3A → Shard 0 (0x00~0x7F 담당)
customer_id=5678 → hash → 0xB2 → Shard 1 (0x80~0xFF 담당)
```

**Resharding (샤드 분할)**도 온라인 상태에서 점진적으로 처리할 수 있습니다.

```
1개 샤드 (0x00~0xFF)
    ↓ 분할
Shard 0 (0x00~0x7F) + Shard 1 (0x80~0xFF)
    ↓ Shard 0만 추가 분할
Shard 0a (0x00~0x3F) + Shard 0b (0x40~0x7F) + Shard 1 (0x80~0xFF)
```

서비스 중단 없이 점진적으로 쪼갤 수 있다는 게 큰 장점입니다.

---

## 주요 기능

**커넥션 폭발 문제 해결**

MySQL은 커넥션 하나당 메모리를 꽤 소비합니다. 앱 서버가 많아지면 커넥션 수가 폭발합니다.

```
Vitess 없이: 앱 100대 × 50 커넥션 = 5,000개 → MySQL 과부하

Vitess 도입: 앱 → VTGate (5,000 커넥션 수용)
                  VTTablet → MySQL (50~100 커넥션만 유지)
```

**Cross-shard 집계 쿼리 자동 처리**

```sql
-- 앱은 그냥 이렇게만 씀
SELECT COUNT(*) FROM orders;

-- Vitess 내부 동작
-- Shard 0 → COUNT(*) = 150,000
-- Shard 1 → COUNT(*) = 170,000
-- VTGate에서 320,000으로 합산 후 반환
```

**온라인 DDL**

MySQL의 고질적 문제인 스키마 변경 시 테이블 락을 우회합니다.

```sql
-- 일반 MySQL: 수억 건 테이블에 컬럼 추가 → 수 시간 테이블 잠금 😱
ALTER TABLE orders ADD COLUMN memo TEXT;

-- Vitess: 백그라운드에서 점진적으로 처리, 서비스 영향 없음 ✅
```

내부적으로 gh-ost 또는 pt-online-schema-change 방식을 활용합니다.

**자동 Failover**

```
Primary MySQL 장애 발생
    → VTTablet이 헬스체크 실패 감지
    → VTCtld에 보고
    → 복제 지연이 가장 적은 Replica를 새 Primary로 자동 승격
    → VTGate가 새 Primary 주소 자동 인식
    → 앱은 짧은 에러 후 자동 재연결
```

---

## 단점

수평 확장 문제를 해결해주지만 그만큼 운영 복잡도가 높습니다. VTGate, VTTablet, VTCtld, etcd를 모두 관리해야 하고, 러닝 커브가 가파릅니다. 또한 MySQL 전용이라 PostgreSQL에는 사용할 수 없습니다 (그래서 PgDog 같은 프로젝트가 나온 이유입니다). Cross-shard 트랜잭션은 여전히 완전한 ACID 보장이 어렵습니다.

---

## 사용 기업

Vitess를 만든 **YouTube**를 비롯해, **Slack**(수백억 건 메시지 저장), **GitHub**, **Square**(결제 데이터) 등이 사용하고 있습니다. **PlanetScale**은 아예 Vitess를 기반으로 DBaaS 서비스를 만들었습니다.

---

## 공부 관점 요약

```
Vitess가 해결하는 핵심 문제 3가지

1. 커넥션 폭발   → VTGate 커넥션 풀링
2. 샤딩 복잡도   → SQL 파싱 + 자동 라우팅으로 앱 코드 변경 최소화
3. 운영 안정성   → 자동 Failover, 온라인 DDL, 온라인 Resharding
```
