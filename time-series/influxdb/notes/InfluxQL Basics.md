# InfluxQL 기초 정리

InfluxQL은 InfluxDB의 기본 쿼리 언어로, SQL과 유사한 문법을 사용하여 시계열 데이터를 조회, 집계, 필터링할 수 있도록 설계되었습니다. 특히 InfluxDB 1.x 및 3.x에서 적극적으로 사용됩니다.

---

## 1. 기본 SELECT 문법

```sql
SELECT <field> FROM <measurement> WHERE <조건> GROUP BY <시간 또는 태그>
```

### 예시

```sql
SELECT usage_idle FROM cpu WHERE time >= now() - 1h
```

* `cpu`: measurement 이름
* `usage_idle`: field (실제 값)
* `time`: 시계열 기준 필드 (예약어)

---

## 2. 시간 필터링 (time)

* `time`은 InfluxDB의 모든 쿼리에서 핵심 필드입니다

### 예시

```sql
WHERE time >= now() - 1d
WHERE time >= '2025-06-01T00:00:00Z' AND time < '2025-06-02T00:00:00Z'
```

---

## 3. GROUP BY time()

* 일정한 시간 간격으로 데이터를 집계할 때 사용합니다

### 예시

```sql
SELECT mean(usage_idle) FROM cpu WHERE time >= now() - 1d GROUP BY time(10m)
```

> 10분 단위 평균값을 출력

---

## 4. 필드 vs 태그

| 요소        | 인덱스 | 설명         | 예시                                   |
| --------- | --- | ---------- | ------------------------------------ |
| **Field** | ❌   | 측정 값       | `usage_idle`, `temp`                 |
| **Tag**   | ✅   | 필터링용 메타데이터 | `host='server1'`, `region='us-west'` |

쿼리 최적화를 위해 자주 필터링하는 조건은 **Tag**로 설정하는 것이 좋습니다.

---

## 5. 주요 집계 함수

| 함수                     | 설명      |
| ---------------------- | ------- |
| `mean()`               | 평균      |
| `sum()`                | 합계      |
| `min()` / `max()`      | 최소/최대   |
| `count()`              | 개수      |
| `derivative()`         | 변화율 계산  |
| `percentile(field, N)` | N 백분위 수 |

---

## 6. 정렬 및 제한

```sql
ORDER BY time DESC LIMIT 10
```

* 최신 순 정렬 후 상위 10개 데이터 조회

---

## 7. Retention Policy와의 연결

```sql
SELECT * FROM "7d_rp"."cpu" WHERE time >= now() - 7d
```

* `"7d_rp"`: 7일 보존 정책 이름 (Retention Policy)
* `"cpu"`: Measurement 이름

---

## 8. Continuous Query (CQ)

* 주기적으로 실행되어 집계 데이터를 생성하는 쿼리

```sql
CREATE CONTINUOUS QUERY cq_avg ON mydb BEGIN
  SELECT mean(usage_idle) INTO cpu_10m_avg
  FROM cpu GROUP BY time(10m)
END
```
