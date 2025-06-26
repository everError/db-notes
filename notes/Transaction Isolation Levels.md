# 트랜잭션 격리 수준 (Transaction Isolation Levels)

트랜잭션 격리 수준은 여러 트랜잭션이 동시에 실행될 때 \*\*데이터 일관성(consistency)\*\*과 **동시성(concurrency)** 사이의 균형을 조절하는 방식입니다. 각 수준은 특정한 \*\*현상(phenomena)\*\*을 허용하거나 방지합니다.

---

## 1. Read Uncommitted (읽기 미확정)

- **설명**: 커밋되지 않은 데이터를 읽을 수 있음.
- **허용되는 현상**: Dirty Read, Non-repeatable Read, Phantom Read
- **장점**: 동시성 가장 높음
- **단점**: 데이터 일관성 보장 거의 없음

```
현상 예: 트랜잭션 A가 수정했지만 아직 커밋하지 않은 데이터를 트랜잭션 B가 읽음 → 나중에 A가 롤백하면 B는 잘못된 데이터를 읽은 것.
```

---

## 2. Read Committed (읽기 확정)

- **설명**: 커밋된 데이터만 읽을 수 있음.
- **허용되는 현상**: Non-repeatable Read, Phantom Read
- **방지되는 현상**: Dirty Read
- **기본 수준**: SQL Server, Oracle (기본 격리 수준)

```
현상 예: 같은 SELECT 쿼리를 여러 번 실행할 때 그 사이에 다른 트랜잭션이 커밋하면 결과가 달라질 수 있음.
```

---

## 3. Repeatable Read (반복 가능 읽기)

- **설명**: 같은 쿼리는 항상 같은 결과를 보장 (행 수준 잠금)
- **허용되는 현상**: Phantom Read
- **방지되는 현상**: Dirty Read, Non-repeatable Read
- **기본 수준**: MySQL (InnoDB의 기본)

```
현상 예: SELECT한 행은 다른 트랜잭션에서 UPDATE나 DELETE할 수 없음. 하지만 새로운 행 INSERT는 감지 못함 → Phantom Read 가능.
```

---

## 4. Serializable (직렬화 가능)

- **설명**: 트랜잭션을 완전히 순차적으로 실행한 것처럼 보이게 함
- **모든 현상 방지**: Dirty Read, Non-repeatable Read, Phantom Read
- **가장 높은 격리 수준**, 가장 낮은 동시성

```
현상 예: SELECT한 조건 범위에 새로운 행을 INSERT도 못 하게 막음 (범위 잠금)
```

---

## ✅ 정리: 격리 수준별 현상 허용 여부

| 격리 수준        | Dirty Read | Non-repeatable Read | Phantom Read |
| ---------------- | ---------- | ------------------- | ------------ |
| Read Uncommitted | 허용       | 허용                | 허용         |
| Read Committed   | 방지       | 허용                | 허용         |
| Repeatable Read  | 방지       | 방지                | 허용         |
| Serializable     | 방지       | 방지                | 방지         |

---

## 📌 참고사항

- 일부 DBMS는 `Snapshot` 격리 수준을 지원함 (예: SQL Server의 `READ_COMMITTED_SNAPSHOT`, PostgreSQL의 MVCC 기반)
- 애플리케이션 성능과 데이터 정확성 요구사항에 따라 격리 수준 선택 필요
