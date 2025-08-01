### **SQL Server Computed Column**

---

#### **1. 정의**

\*\*Computed Column(계산 열)\*\*은 테이블의 다른 열, 상수, 함수 등을 기반으로 계산된 값을 가지는 열입니다. 이 값은 정의된 식(expression)에 따라 자동으로 계산되며, 별도의 데이터를 입력할 필요가 없습니다.

#### **2. 특징 및 동작 방식**

- **계산식 기반**: 하나 이상의 열을 사용한 수학적 연산, 문자열 연결, 함수 호출 등 다양한 식을 정의할 수 있습니다.
- **데이터 타입**: 계산식의 결과에 따라 자동으로 데이터 타입이 결정됩니다.
- **저장 방식**:
  - **가상(Virtual)**: 기본적으로 물리적으로 저장되지 않습니다. 쿼리가 실행될 때마다 실시간으로 계산됩니다.
  - **영구(Persisted)**: `PERSISTED` 키워드를 사용하면 계산 결과가 테이블에 물리적으로 저장됩니다.
- **인덱스**: `PERSISTED`로 정의된 계산 열은 인덱스를 생성할 수 있어 쿼리 성능 개선에 활용됩니다.
- **제약 조건**: `DEFAULT`, `CHECK`와 같은 제약 조건은 직접 적용할 수 없습니다.

#### **3. 사용 예시**

다음은 `Orders` 테이블에 `TotalPrice`라는 계산 열을 추가하는 예시입니다.

```sql
CREATE TABLE Orders (
    OrderID INT PRIMARY KEY,
    Quantity INT,
    UnitPrice MONEY,
    TotalPrice AS Quantity * UnitPrice -- 계산 열 정의
);
```

`TotalPrice`를 물리적으로 저장하고 인덱싱이 가능하도록 하려면 `PERSISTED` 키워드를 사용합니다.

```sql
CREATE TABLE Orders (
    OrderID INT PRIMARY KEY,
    Quantity INT,
    UnitPrice MONEY,
    TotalPrice AS Quantity * UnitPrice PERSISTED -- 영구 저장
);
```

#### **4. 성능 개선 활용**

Computed Column은 특히 복잡한 계산이 포함된 쿼리의 성능을 향상시키는 데 효과적입니다.

- **인덱스를 통한 검색 속도 향상**: `PERSISTED`로 정의된 Computed Column에 **인덱스**를 생성할 수 있습니다. 복잡한 계산이 포함된 `WHERE` 절이나 `ORDER BY` 절이 있을 때, 인덱스를 활용하여 테이블 전체를 스캔하는 대신 빠르게 데이터를 찾을 수 있습니다.
- **쿼리 단순화**: 복잡한 계산식을 직접 쿼리에 작성하는 대신, 계산 열을 만들어 `WHERE` 절을 단순화할 수 있습니다.
- **주의 사항**: `PERSISTED`로 설정된 계산 열은 데이터가 물리적으로 저장되므로, `INSERT` 및 `UPDATE` 작업 시 약간의 오버헤드가 발생할 수 있습니다. 성능 개선 효과와 오버헤드를 고려하여 신중하게 사용해야 합니다.
