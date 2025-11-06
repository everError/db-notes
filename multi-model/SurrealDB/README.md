# SurrealDB 조사 문서

## 개요
SurrealDB는 현대 애플리케이션을 위한 엔드투엔드 클라우드 네이티브 데이터베이스로, 웹, 모바일, 서버리스, Jamstack, 백엔드 및 전통적인 애플리케이션을 모두 지원합니다. Rust로 작성되었으며, 2022년에 등장한 차세대 멀티모델 데이터베이스입니다.

## 핵심 특징

### 1. 멀티모델 지원
SurrealDB는 멀티모델 데이터베이스로서 여러 데이터 모델을 하나의 데이터베이스에서 동시에 사용할 수 있습니다:

- **관계형(Relational)**: 테이블 기반 데이터, 선택적 스키마
- **문서형(Document)**: JSON 필드, 무한 중첩 객체
- **그래프(Graph)**: 네이티브 엣지와 버텍스, 그래프 순회
- **Key-Value**: 간단한 키-값 쌍 저장
- **시계열(Time-Series)**: 시간 기반 데이터 저장

사전에 데이터 모델링 방식을 선택할 필요가 없으며, 필요에 따라 유연하게 변경할 수 있습니다.

### 2. SurrealQL - 강력한 쿼리 언어

#### SQL 호환성
SurrealQL은 SQL과 유사한 문법을 제공하면서도 훨씬 더 강력합니다:

```sql
-- 기본 CRUD
CREATE person SET name = 'John', age = 30;
SELECT * FROM person WHERE age > 25;
UPDATE person:john SET age = 31;
DELETE person:old_user;

-- 서브쿼리 및 조건문
SELECT * FROM user WHERE 
    (SELECT count() FROM post WHERE author = user.id) > 10;
```

#### 그래프 쿼리 내장
그래프 관계를 SQL 스타일로 간단하게 표현:

```sql
-- 화살표 문법으로 그래프 탐색
SELECT ->follows->user->posts FROM user:john;

-- 양방향 탐색
SELECT <-purchased<-user FROM product:laptop;

-- 복잡한 그래프 분석
SELECT 
    ->friend->person<-friend<-person 
    AS mutual_friends 
FROM person:alice;
```

#### 고급 기능
- **RELATE 문**: 그래프 엣지 생성
- **FETCH 절**: 관련 레코드 자동 로드
- **SPLIT 절**: 배열 데이터 분할
- **GROUP BY**: 집계 쿼리
- **DEFINE 문**: 스키마, 인덱스, 이벤트, 함수 등 정의

### 3. 실시간 기능

#### Live Queries
클라이언트가 쿼리를 구독하여 실시간 업데이트를 받을 수 있습니다:

**특징:**
- 테이블/레코드/필드 레벨 권한 자동 적용
- 권한 변경 시 즉시 반영 (재구독 불필요)
- CREATE, UPDATE, DELETE 이벤트 감지
- 전체 레코드 또는 DIFF만 전송 가능

**작동 방식:**
```sql
-- 채팅방 메시지 실시간 구독
LIVE SELECT author, message, timestamp 
FROM chat_table 
WHERE room = "general";

-- 결과: UUID 반환 (예: "30eaa4fb-3fc7-45d6-baa0-cb79121ffcc4")
```

클라이언트는 WebSocket을 통해 실시간 알림을 받습니다:
- **CREATE**: 새 레코드 추가
- **UPDATE**: 레코드 수정
- **DELETE**: 레코드 삭제
- **CLOSE**: 쿼리 종료

#### Change Feeds
과거 변경 이력을 저장하고 재생:

```sql
-- Change Feed 정의 (3일간 보관)
DEFINE TABLE orders CHANGEFEED 3d;

-- 특정 시점 이후 변경사항 조회
SHOW CHANGES FOR TABLE orders 
SINCE d"2025-01-01T00:00:00Z" 
LIMIT 100;
```

**Live Queries vs Change Feeds:**
- Live Queries: 현재 변경사항만, 테이블 레벨 동작
- Change Feeds: 과거 이력 포함, 데이터베이스/테이블 레벨 동작

### 4. AI/ML 통합

#### 벡터 검색
네이티브 벡터 유사도 검색 지원:

```sql
-- 벡터 임베딩 저장
CREATE product:laptop SET 
    name = 'MacBook Pro',
    embedding = [0.1, 0.2, 0.3, ...]; -- 벡터

-- 코사인 유사도 검색
SELECT *, 
    vector::similarity::cosine(embedding, $query_vector) AS score 
FROM product 
WHERE embedding <|10|> $query_vector
ORDER BY score DESC;
```

#### KNN (K-Nearest Neighbors) 연산자
새로운 `<|K|>` 연산자로 K개의 가장 유사한 벡터 검색:

```sql
-- 상위 5개 유사 제품
SELECT * FROM product 
WHERE embedding <|5|> [0.1, 0.2, 0.3];
```

#### AI 프레임워크 통합
- **LangChain**: 벡터 스토어로 사용 가능
- **LlamaIndex**: 데이터 인덱싱 및 검색
- **CrewAI**: 에이전트 메모리 저장
- **OpenAI, Mistral, Together AI**: 임베딩 생성 연동

#### RAG (Retrieval-Augmented Generation) 최적화
```sql
-- 벡터 검색 + 그래프 관계 + 지리공간 쿼리 결합
SELECT 
    supplier.{name, address},
    vector::similarity::cosine(embedding, $ideal_spec) AS score
FROM manufacturer
WHERE geo::distance(location, $site) < 10000
ORDER BY score DESC
FETCH contacts;
```

### 5. 배포 유연성

#### 임베디드 모드
애플리케이션에 직접 통합:

```rust
// Rust 예시
use surrealdb::engine::local::Mem;
let db = Surreal::new::<Mem>(()).await?;
```

```javascript
// JavaScript/Node.js 예시
import { Surreal } from 'surrealdb';
const db = new Surreal();
await db.connect('memory');
```

#### 단일 노드 서버
```bash
# RocksDB 사용
surreal start --log debug file://mydatabase.db

# 메모리 모드
surreal start memory
```

#### 분산 클러스터
```bash
# TiKV 클러스터와 연결
surreal start tikv://127.0.0.1:2379
```

#### 클라우드 (Surreal Cloud)
- 완전 관리형 서비스
- 자동 백업 및 모니터링
- 글로벌 배포
- 엔터프라이즈 지원

### 6. 멀티테넌시 (Multi-Tenancy)

SurrealDB는 멀티테넌트 아키텍처를 네이티브로 지원:

```
Root
├── Namespace (조직/부서 분리)
│   ├── Database 1
│   │   ├── Table A
│   │   └── Table B
│   └── Database 2
└── Namespace 2
```

각 레벨에서 독립적인 사용자 및 권한 관리가 가능합니다.

### 7. 개발자 경험

#### 직접 클라이언트 연결
프론트엔드에서 직접 데이터베이스 연결:

```javascript
import { Surreal } from 'surrealdb';

const db = new Surreal();
await db.connect('wss://cloud.surrealdb.com/rpc');

// 네임스페이스/데이터베이스 선택
await db.use({ ns: 'myapp', db: 'production' });

// 사용자 인증
await db.signin({
    username: 'user@example.com',
    password: 'password'
});

// 쿼리 실행
const users = await db.select('user');
```

#### WebAssembly 지원
브라우저에서 SurrealDB 실행:

```javascript
import Surreal from '@surrealdb/wasm';

const db = new Surreal();
// IndexedDB에 영속화
await db.connect('indxdb://mydb');
```

#### 다양한 SDK
- JavaScript/TypeScript (NPM, JSR)
- Rust (native)
- Python (async/sync)
- Go
- Java
- .NET (C#)
- PHP
- Deno
- Bun

### 8. 이벤트 및 트리거

데이터 변경 시 자동으로 로직 실행:

```sql
-- 이벤트 정의
DEFINE EVENT email_notification ON TABLE user
WHEN $event = "CREATE"
THEN {
    -- 이메일 발송 함수 호출
    http::post('https://api.sendgrid.com/v3/mail/send', {
        headers: { 'Authorization': 'Bearer ' + $token },
        body: {
            to: $after.email,
            subject: 'Welcome!',
            text: 'Thank you for joining!'
        }
    });
};
```

### 9. 인덱싱

#### 인덱스 타입
- **일반 인덱스**: 빠른 조회
- **유니크 인덱스**: 중복 방지
- **풀텍스트 인덱스**: 전문 검색
- **벡터 인덱스**: 벡터 검색 최적화

```sql
-- 일반 인덱스
DEFINE INDEX user_email ON user FIELDS email;

-- 유니크 인덱스
DEFINE INDEX unique_email ON user FIELDS email UNIQUE;

-- 풀텍스트 검색 인덱스
DEFINE INDEX post_content ON post 
FIELDS title, content 
SEARCH ANALYZER ascii 
BM25 HIGHLIGHTS;

-- 복합 인덱스
DEFINE INDEX user_location ON user 
FIELDS city, country;
```

## 주요 기능

### 1. 보안 및 권한 관리

#### RBAC (Role-Based Access Control)
SurrealDB는 시스템 사용자를 위한 역할 기반 접근 제어를 구현합니다:
- **OWNER**: 모든 리소스 보기/편집 및 사용자/IAM 리소스 생성 가능
- **EDITOR**: 해당 레벨의 모든 리소스 보기/편집 가능
- **VIEWER**: 해당 레벨의 리소스를 읽기 전용으로만 접근 가능

```sql
-- 데이터베이스 레벨 사용자 정의
DEFINE USER db_viewer ON DATABASE PASSWORD 'password' ROLES VIEWER;
DEFINE USER db_admin ON DATABASE PASSWORD 'password' ROLES OWNER;
```

#### 세밀한 권한 제어
테이블, 레코드, 필드 레벨에서 권한을 정의할 수 있습니다:

```sql
-- 테이블 레벨 권한
DEFINE TABLE post SCHEMALESS 
PERMISSIONS 
    FOR select WHERE published = true OR user = $auth.id
    FOR create, update WHERE user = $auth.id
    FOR delete WHERE user = $auth.id OR $auth.admin = true;

-- 필드 레벨 권한
DEFINE TABLE user SCHEMAFULL
PERMISSIONS FOR select, update, delete WHERE id = $auth.id;

DEFINE FIELD password ON user TYPE string
PERMISSIONS NONE; -- 비밀번호 필드는 접근 불가
```

#### 레코드 사용자 (Record Users)
애플리케이션 사용자를 위한 커스텀 인증:

```sql
-- 사용자 테이블 정의
DEFINE TABLE user SCHEMAFULL
PERMISSIONS FOR select, update, delete WHERE id = $auth.id;

DEFINE FIELD email ON user TYPE string 
ASSERT string::is_email($value);

-- 레코드 액세스 정의 (회원가입/로그인)
DEFINE ACCESS user ON DATABASE TYPE RECORD
SIGNIN (
    SELECT * FROM user 
    WHERE email = $email 
    AND crypto::argon2::compare(password, $password)
)
SIGNUP (
    CREATE user CONTENT {
        name: $name,
        email: $email,
        password: crypto::argon2::generate($password)
    }
);
```

#### 암호화 함수
- `crypto::argon2::*` - Argon2 해싱
- `crypto::bcrypt::*` - Bcrypt 해싱
- `crypto::pbkdf2::*` - PBKDF2 해싱
- `crypto::scrypt::*` - Scrypt 해싱

### 2. 그래프 데이터베이스 기능

#### 레코드 링크
복잡한 JOIN 없이 레코드 간 관계를 표현:

```sql
-- 사용자 생성
CREATE user:john SET name = 'John', age = 30;
CREATE user:jane SET name = 'Jane', age = 28;

-- 관계 생성 (그래프 엣지)
RELATE user:john->follows->user:jane;
RELATE user:john->follows->user:bob;

-- 엣지에 속성 추가
RELATE user:john->likes->post:123 SET liked_at = time::now();
```

#### 그래프 탐색
간단한 화살표 문법으로 그래프를 탐색:

```sql
-- 존이 팔로우하는 사람들
SELECT ->follows->user.name FROM user:john;

-- 존을 팔로우하는 사람들
SELECT <-follows<-user.name FROM user:john;

-- 친구의 친구
SELECT ->follows->user->follows->user.name FROM user:john;

-- 추천 시스템: 같은 제품을 구매한 다른 사용자들이 구매한 제품
SELECT 
    ->purchased->product<-purchased<-user->purchased->product 
WHERE product != product:p123 
GROUP BY product 
FROM user:john;
```

### 3. 실시간 기능

#### Live Queries
데이터 변경 시 실시간으로 클라이언트에 푸시:

```sql
-- 기본 Live Query
LIVE SELECT * FROM person WHERE age > 25;

-- 특정 필드만 선택
LIVE SELECT name, email FROM user WHERE active = true;

-- DIFF 모드 (변경사항만 전송)
LIVE SELECT DIFF FROM person WHERE age > 18;
```

JavaScript SDK 사용 예시:
```javascript
// Live Query 시작
const queryUuid = await db.live(
    "person",
    (action, result) => {
        // action: 'CREATE', 'UPDATE', 'DELETE', 'CLOSE'
        if (action === 'CREATE') {
            console.log('새 레코드:', result);
        }
        if (action === 'UPDATE') {
            console.log('업데이트:', result);
        }
        if (action === 'DELETE') {
            console.log('삭제:', result.id);
        }
    }
);

// Live Query 종료
await db.kill(queryUuid);
```

#### Change Feeds
과거 변경 이력을 조회:

```sql
-- 테이블에 Change Feed 정의 (3일간 보관)
DEFINE TABLE reading CHANGEFEED 3d;

-- 특정 시점 이후의 변경사항 조회
SHOW CHANGES FOR TABLE reading 
SINCE d"2023-09-07T01:23:52Z" 
LIMIT 10;
```

### 4. 임베디드 함수 및 스크립팅

#### ES2020 JavaScript 함수
데이터베이스 내에서 JavaScript 코드 실행:

```sql
-- 인라인 JavaScript 함수
CREATE film SET 
    ratings = [
        { rating: 6, user: user:alice },
        { rating: 8, user: user:bob }
    ],
    featured = function() {
        return this.ratings
            .filter(r => r.rating >= 7)
            .map(r => ({ ...r, rating: r.rating * 10 }));
    };

-- 커스텀 함수 정의
DEFINE FUNCTION distance($lat1, $lon1, $lat2, $lon2) {
    RETURN function::js('
        const R = 6371;
        const dLat = (lat2 - lat1) * Math.PI / 180;
        const dLon = (lon2 - lon1) * Math.PI / 180;
        const a = Math.sin(dLat/2) * Math.sin(dLat/2) +
                  Math.cos(lat1 * Math.PI/180) * Math.cos(lat2 * Math.PI/180) *
                  Math.sin(dLon/2) * Math.sin(dLon/2);
        return 2 * R * Math.asin(Math.sqrt(a));
    ', { lat1: $lat1, lon1: $lon1, lat2: $lat2, lon2: $lon2 });
};
```

#### 내장 함수 카테고리
- **Array 함수**: `array::add()`, `array::all()`, `array::filter()`, `array::map()` 등
- **Math 함수**: `math::abs()`, `math::ceil()`, `math::sqrt()`, `math::max()` 등
- **String 함수**: `string::capitalize()`, `string::is::email()`, `string::trim()` 등
- **Time 함수**: `time::now()`, `time::day()`, `time::format()` 등
- **Crypto 함수**: 해싱, 암호화 함수
- **Geo 함수**: `geo::area()`, `geo::distance()` 등
- **Vector 함수**: `vector::similarity::cosine()`, `vector::distance::euclidean()` 등
- **HTTP 함수**: `http::get()`, `http::post()` 등

### 5. 스토리지 엔진

SurrealDB는 다양한 환경에서 실행 가능하도록 여러 스토리지 엔진을 지원합니다:

#### 단일 노드 스토리지
- **Memory (메모리)**: 인메모리 실행, 재시작 시 데이터 소실
- **RocksDB**: 파일 기반, Meta(Facebook)가 개발한 고성능 키-값 저장소
  - LSM(Log-Structured Merge-Tree) 기반
  - SSD 최적화
  - 기본 스토리지 엔진
- **SurrealKV** (실험적): SurrealDB 자체 개발 스토리지 엔진
  - Rust로 작성
  - VART(Versioned Adaptive Radix Trie) 데이터 구조
  - 시간 여행 쿼리 지원 (과거 데이터 조회)
  - 불변 데이터 쿼리
  - 버전별 그래프 쿼리

#### 분산 스토리지
- **TiKV**: 고가용성 분산 키-값 저장소
  - Raft 합의 알고리즘
  - ACID 트랜잭션
  - 페타바이트급 확장 가능
- **FoundationDB**: Apple이 개발한 분산 데이터베이스

#### 브라우저 스토리지
- **IndexedDB**: 웹 브라우저 내 데이터 영속화
  - WebAssembly로 SurrealDB 실행
  - 오프라인 지원
  - 브라우저 저장소 활용

### 6. 트랜잭션
완전한 ACID 트랜잭션을 지원하여 데이터 일관성을 보장합니다.

```sql
BEGIN TRANSACTION;

-- 여러 작업 수행
CREATE account:john SET balance = 1000;
CREATE account:jane SET balance = 500;

UPDATE account:john SET balance = balance - 100;
UPDATE account:jane SET balance = balance + 100;

COMMIT TRANSACTION;
```

### 7. 고급 데이터 타입

#### 기본 타입
- **문자열, 숫자, 불린**: 표준 타입
- **배열 및 객체**: 무한 중첩 가능
- **Null / None**: 값 없음 표현

#### 특수 타입
- **Duration**: 나노초에서 주 단위까지
  ```sql
  CREATE event SET duration = 2h30m;
  ```

- **DateTime**: ISO-8601 형식, UTC 자동 변환
  ```sql
  CREATE post SET created_at = time::now();
  ```

- **GeoJSON**: 지리공간 데이터
  ```sql
  UPDATE city:london SET 
      centre = (-0.118092, 51.509865),
      boundary = {
          type: "Polygon",
          coordinates: [[
              [-0.38314819, 51.37692386],
              [0.1785278, 51.37692386],
              [0.1785278, 51.61460570],
              [-0.38314819, 51.61460570],
              [-0.38314819, 51.37692386]
          ]]
      };
  ```

- **Record ID**: 타입 안전한 레코드 참조
  ```sql
  CREATE post SET author = user:john;
  ```

## 실제 활용 사례

### 산업별 활용
소매/전자상거래에서는 벡터 검색, 사용자 그래프, 실시간 시그널을 활용한 개인화된 피드와 추천 시스템에 사용됩니다.

헬스케어에서는 필드 레벨 보안과 시간 여행 쿼리를 통해 환자, 기록, 센서 데이터를 모델링하며, 데이터 무결성과 규정 준수에 적합합니다.

생성형 AI 분야에서는 벡터 검색, 그래프, 트랜잭션 업데이트를 하나의 쿼리로 처리하여 RAG, 에이전트 메모리, 실시간 LLM 앱 구축에 이상적입니다.

## 성능 및 성숙도

2025년 기준 OLTP 워크로드에서 PostgreSQL과 동등하거나 더 나은 성능을 보이며, 쓰기 성능은 PostgreSQL과 거의 같고 읽기 성능은 내부 테스트에서 더 빠릅니다.

기업용 준비 단계에 있으며, 중소규모 기업 애플리케이션, 특히 멀티모델이나 실시간 기능이 필요한 경우 강력한 경쟁자입니다. 스타트업과 소규모 애플리케이션에서는 개발 가속화와 복잡성 감소로 뛰어난 성능을 발휘합니다.

## 개발자 도구

Surrealist GUI(공식 관리 스튜디오), 다양한 언어의 SDK(JavaScript/TypeScript, Rust, Go, Python, Java, .NET, PHP 등), SurrealDB CLI, SurrealDB University 학습 자료 및 AI "Sidekick"을 제공합니다.

## 설치 방법

```bash
# Docker 사용
docker pull surrealdb/surrealdb:latest
docker run --rm -p 8000:8000 surrealdb/surrealdb:latest start

# macOS (Homebrew)
brew install surrealdb

# 설치 후 접속
http://localhost:8000
```

, $ideal_spec) AS score 
FROM manufacturer 
WHERE geo::distance(location, $site) < 10000 
ORDER BY score DESC;

-- 레코드 사용자 정의 (회원가입/로그인)
DEFINE ACCESS user_access ON DATABASE TYPE RECORD
SIGNIN (
    SELECT * FROM user 
    WHERE email = $email 
    AND crypto::argon2::compare(password, $password)
)
SIGNUP (
    CREATE user CONTENT {
        email: $email,
        password: crypto::argon2::generate($password),
        created_at: time::now()
    }
);
```

### 이벤트 및 트리거

```sql
-- 사용자 생성 시 환영 이메일 발송
DEFINE EVENT welcome_email ON TABLE user
WHEN $event = "CREATE"
THEN {
    -- 외부 API 호출
    http::post('https://api.example.com/send-email', {
        to: $after.email,
        subject: 'Welcome!',
        body: 'Thanks for joining!'
    });
};

-- 주문 생성 시 재고 감소
DEFINE EVENT update_inventory ON TABLE order
WHEN $event = "CREATE"
THEN {
    UPDATE product 
    SET stock = stock - $after.quantity 
    WHERE id = $after.product_id;
};
```

### 커스텀 함수

```sql
-- 거리 계산 함수
DEFINE FUNCTION fn::calculate_distance($lat1, $lon1, $lat2, $lon2) {
    RETURN function::js('
        const R = 6371; // 지구 반지름 (km)
        const dLat = (lat2 - lat1) * Math.PI / 180;
        const dLon = (lon2 - lon1) * Math.PI / 180;
        const a = Math.sin(dLat/2) * Math.sin(dLat/2) +
                  Math.cos(lat1 * Math.PI/180) * Math.cos(lat2 * Math.PI/180) *
                  Math.sin(dLon/2) * Math.sin(dLon/2);
        return 2 * R * Math.asin(Math.sqrt(a));
    ', { lat1: $lat1, lon1: $lon1, lat2: $lat2, lon2: $lon2 });
};

-- 사용
SELECT *, 
    fn::calculate_distance(latitude, longitude, 37.5665, 126.9780) AS distance_km
FROM store
ORDER BY distance_km
LIMIT 10;
```

### JavaScript SDK 사용 예시

```javascript
import { Surreal } from 'surrealdb';

const db = new Surreal();

// 연결
await db.connect('ws://localhost:8000/rpc');

// 인증
await db.signin({
    username: 'root',
    password: 'root'
});

// 네임스페이스/데이터베이스 선택
await db.use({ ns: 'myapp', db: 'production' });

// 데이터 생성
const user = await db.create('user', {
    name: 'John Doe',
    email: '[email protected]',
    age: 30
});

// 조회
const users = await db.select('user');
const john = await db.select('user:john');

// 쿼리 실행
const result = await db.query(`
    SELECT * FROM user 
    WHERE age > $age 
    ORDER BY created_at DESC
`, {
    age: 25
});

// Live Query
const queryUuid = await db.live('user', (action, result) => {
    console.log(`${action}:`, result);
});

// 종료
await db.kill(queryUuid);
await db.close();
```

### React 사용 예시

```javascript
import { useEffect, useState } from 'react';
import { Surreal } from 'surrealdb';

function UserList() {
    const [users, setUsers] = useState([]);
    const [db] = useState(() => new Surreal());

    useEffect(() => {
        async function setupLiveQuery() {
            await db.connect('ws://localhost:8000/rpc');
            await db.use({ ns: 'app', db: 'main' });
            
            // 초기 데이터 로드
            const initial = await db.select('user');
            setUsers(initial);
            
            // Live Query 설정
            await db.live('user', (action, result) => {
                setUsers(prev => {
                    if (action === 'CREATE') {
                        return [...prev, result];
                    }
                    if (action === 'UPDATE') {
                        return prev.map(u => 
                            u.id === result.id ? result : u
                        );
                    }
                    if (action === 'DELETE') {
                        return prev.filter(u => u.id !== result.id);
                    }
                    return prev;
                });
            });
        }
        
        setupLiveQuery();
        
        return () => db.close();
    }, [db]);

    return (
        <div>
            <h1>실시간 사용자 목록</h1>
            <ul>
                {users.map(user => (
                    <li key={user.id}>{user.name}</li>
                ))}
            </ul>
        </div>
    );
}
```
```

## 장점

1. **단일 데이터베이스로 모든 것 해결** - 여러 데이터베이스와 API 스택을 단순화
2. **실시간 협업 기능** - 네이티브 실시간 데이터 동기화
3. **개발 시간 단축** - 백엔드 코드와 보안 규칙 구현 필요 감소
4. **AI/ML 친화적** - 벡터 검색과 임베딩 네이티브 지원
5. **유연한 배포** - 임베디드부터 클라우드까지 다양한 환경 지원

## 고려사항

상대적으로 신생 데이터베이스이므로 오랜 운영 실적이 부족하고, 일부 틈새 기능이 없으며, BSL 라이선스를 사용합니다. 엔터프라이즈 도입 시에는 커뮤니티 지원과 안정성을 신중히 평가해야 합니다.

## 적합한 사용 사례

- 실시간 애플리케이션 (채팅, 협업 도구)
- 소셜 네트워크 및 지식 그래프
- AI/ML 애플리케이션 (RAG, 추천 시스템)
- IoT 및 엣지 컴퓨팅
- 빠른 프로토타이핑 및 스타트업
- 서버리스 및 Jamstack 애플리케이션

---

## 참고 자료
- 공식 웹사이트: https://surrealdb.com
- GitHub: https://github.com/surrealdb/surrealdb
- 문서: https://surrealdb.com/docs
- 커뮤니티: Discord, GitHub Discussions