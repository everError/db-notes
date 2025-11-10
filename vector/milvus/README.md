# Milvus 완벽 가이드

## 목차
1. [Milvus 개요](#milvus-개요)
2. [아키텍처](#아키텍처)
3. [핵심 개념](#핵심-개념)
4. [데이터 모델](#데이터-모델)
5. [인덱스 종류](#인덱스-종류)
6. [설치 및 배포](#설치-및-배포)
7. [기본 사용법](#기본-사용법)
8. [고급 기능](#고급-기능)
9. [성능 최적화](#성능-최적화)
10. [모니터링 및 운영](#모니터링-및-운영)

---

## Milvus 개요

### Milvus란?

Milvus는 **오픈소스 벡터 데이터베이스**로, AI 애플리케이션을 위한 임베딩 유사도 검색 및 관리를 제공합니다. LF AI & Data Foundation에서 관리하는 클라우드 네이티브 프로젝트입니다.

### 주요 특징

- **고성능**: 수십억 개의 벡터에서 밀리초 단위 검색
- **확장성**: 수평 확장을 통한 무제한 데이터 처리
- **클라우드 네이티브**: Kubernetes 기반 분산 아키텍처
- **다양한 인덱스**: HNSW, IVF, FLAT 등 10개 이상의 인덱스 알고리즘
- **하이브리드 검색**: 벡터 + 스칼라 필터링 동시 지원
- **멀티 테넌시**: 컬렉션, 파티션을 통한 데이터 격리
- **고가용성**: 분산 아키텍처를 통한 장애 복구

### 사용 사례

- **유사 이미지 검색**: 이미지 임베딩 기반 검색
- **추천 시스템**: 사용자/아이템 임베딩 기반 추천
- **의미 검색**: 텍스트 임베딩을 활용한 문서 검색
- **RAG (Retrieval Augmented Generation)**: LLM을 위한 컨텍스트 검색
- **이상 탐지**: 임베딩 공간에서의 이상치 탐지
- **비디오 분석**: 비디오 프레임 임베딩 검색

---

## 아키텍처

### 전체 구조

Milvus는 **스토리지-컴퓨팅 분리** 아키텍처를 채택했습니다.

```
┌─────────────────────────────────────────────────────────┐
│                    Access Layer                          │
│                  (Proxy / Load Balancer)                 │
└─────────────────────────────────────────────────────────┘
                           │
        ┌──────────────────┼──────────────────┐
        │                  │                  │
┌───────▼────────┐  ┌──────▼──────┐  ┌───────▼────────┐
│ Coordinator    │  │   Worker     │  │   Storage      │
│   Services     │  │    Nodes     │  │    Layer       │
├────────────────┤  ├──────────────┤  ├────────────────┤
│ Root Coord     │  │ Query Node   │  │ MinIO/S3       │
│ Data Coord     │  │ Data Node    │  │ etcd           │
│ Query Coord    │  │ Index Node   │  │ Pulsar/Kafka   │
│ Index Coord    │  │              │  │                │
└────────────────┘  └──────────────┘  └────────────────┘
```

### 주요 컴포넌트

#### 1. Access Layer (접근 계층)

**Proxy**
- 클라이언트 요청의 진입점
- 요청 검증 및 라우팅
- 로드 밸런싱
- 결과 집계 및 후처리

#### 2. Coordinator Services (조정 서비스)

**Root Coordinator (Root Coord)**
- 전역 타임스탬프 할당
- DDL 작업 처리 (컬렉션, 파티션 생성/삭제)
- 스키마 관리

**Data Coordinator (Data Coord)**
- 세그먼트 메타데이터 관리
- 세그먼트 할당 및 병합
- 플러시 작업 스케줄링
- 가비지 컬렉션

**Query Coordinator (Query Coord)**
- Query Node에 세그먼트 할당
- 로드 밸런싱
- 핸드오프 관리 (Growing → Sealed)

**Index Coordinator (Index Coord)**
- 인덱스 빌드 작업 스케줄링
- 인덱스 메타데이터 관리
- Index Node에 작업 할당

#### 3. Worker Nodes (작업 노드)

**Query Node**
- 벡터 검색 실행
- 스칼라 필터링
- 세그먼트 로딩 및 캐싱
- 검색 결과 반환

**Data Node**
- 데이터 삽입 처리
- 로그를 세그먼트로 변환
- 데이터 영속화
- 세그먼트 플러시

**Index Node**
- 인덱스 빌드 실행
- CPU/GPU를 활용한 인덱스 생성
- 빌드된 인덱스를 스토리지에 저장

#### 4. Storage Layer (스토리지 계층)

**Meta Storage (etcd)**
- 컬렉션 스키마
- 세그먼트 메타데이터
- 채널 정보
- 체크포인트

**Message Storage (Pulsar/Kafka)**
- Write-Ahead Log (WAL)
- 데이터 스트림
- 컴포넌트 간 통신

**Object Storage (MinIO/S3)**
- 세그먼트 파일
- 인덱스 파일
- 바이너리 로그

### 데이터 흐름

#### 삽입 (Insert) 흐름

```
Client → Proxy → Data Coord → Pulsar → Data Node → Object Storage
                      ↓
                   etcd (메타데이터)
```

1. 클라이언트가 Proxy에 삽입 요청
2. Proxy가 Data Coord에 세그먼트 할당 요청
3. 데이터가 Pulsar에 기록 (WAL)
4. Data Node가 Pulsar에서 데이터 소비
5. Data Node가 세그먼트를 빌드하고 Object Storage에 저장
6. 메타데이터가 etcd에 업데이트

#### 검색 (Search) 흐름

```
Client → Proxy → Query Coord → Query Node → 결과 집계 → Client
                                    ↓
                              Object Storage
```

1. 클라이언트가 Proxy에 검색 요청
2. Proxy가 Query Coord에 세그먼트 정보 요청
3. Query Node가 메모리에 세그먼트 로드 (필요시)
4. 각 Query Node가 할당된 세그먼트에서 검색 수행
5. Proxy가 결과를 집계하고 정렬
6. 최종 결과를 클라이언트에 반환

---

## 핵심 개념

### 1. Collection (컬렉션)

테이블과 유사한 개념으로, 동일한 스키마를 가진 엔티티들의 집합입니다.

**특징:**
- 명시적 스키마 정의 필요
- 하나 이상의 벡터 필드 포함 가능
- 여러 스칼라 필드 지원
- 파티션으로 세분화 가능

### 2. Partition (파티션)

컬렉션을 논리적으로 분할하는 단위입니다.

**장점:**
- 검색 범위 축소로 성능 향상
- 데이터 격리
- 선택적 로딩/언로딩 가능

**사용 예시:**
- 날짜별 파티션 (2024-01, 2024-02, ...)
- 카테고리별 파티션 (전자기기, 의류, 식품, ...)
- 지역별 파티션 (서울, 부산, 대구, ...)

### 3. Segment (세그먼트)

Milvus의 최소 데이터 저장 단위입니다.

**종류:**

**Growing Segment**
- 메모리에 존재
- 실시간 삽입 수용
- 인덱스 미빌드 상태
- 크기가 제한에 도달하면 Sealed로 전환

**Sealed Segment**
- 더 이상 데이터 추가 불가
- 인덱스 빌드 가능
- Object Storage에 저장
- 검색 최적화됨

**크기:**
- 기본값: 512MB
- 설정 가능 (segment.maxSize)

### 4. Entity (엔티티)

컬렉션 내의 개별 데이터 레코드입니다.

**구성:**
- Primary Key (필수)
- 벡터 필드 (1개 이상)
- 스칼라 필드 (선택)

### 5. Field (필드)

엔티티의 속성입니다.

**필드 타입:**
- Primary Key Field
- Vector Field
- Scalar Field

### 6. Schema (스키마)

컬렉션의 구조를 정의합니다.

```python
schema = CollectionSchema(
    fields=[...],
    description="설명",
    enable_dynamic_field=True  # 동적 필드 활성화
)
```

### 7. Index (인덱스)

벡터 검색 성능을 향상시키는 데이터 구조입니다.

**인덱스 빌드 시점:**
- 세그먼트가 Sealed 상태가 된 후
- 백그라운드에서 자동 빌드
- 수동 빌드 가능

### 8. Consistency Level (일관성 수준)

분산 시스템에서 데이터 일관성 정도를 설정합니다.

**수준:**
- **Strong**: 가장 최신 데이터 보장 (성능 낮음)
- **Bounded**: 특정 시간 이내 데이터
- **Session**: 세션 내에서 일관성
- **Eventually**: 최종 일관성 (성능 높음)

---

## 데이터 모델

### 스키마 정의

```python
from pymilvus import CollectionSchema, FieldSchema, DataType

# 필드 정의
fields = [
    # Primary Key
    FieldSchema(
        name="id",
        dtype=DataType.INT64,
        is_primary=True,
        auto_id=False  # 자동 ID 생성 비활성화
    ),
    
    # 벡터 필드
    FieldSchema(
        name="embedding",
        dtype=DataType.FLOAT_VECTOR,
        dim=768  # 벡터 차원
    ),
    
    # 스칼라 필드
    FieldSchema(
        name="title",
        dtype=DataType.VARCHAR,
        max_length=500
    ),
    
    FieldSchema(
        name="price",
        dtype=DataType.FLOAT
    ),
    
    FieldSchema(
        name="category",
        dtype=DataType.VARCHAR,
        max_length=100
    ),
    
    FieldSchema(
        name="tags",
        dtype=DataType.ARRAY,
        element_type=DataType.VARCHAR,
        max_capacity=10,
        max_length=50
    ),
    
    FieldSchema(
        name="metadata",
        dtype=DataType.JSON
    ),
    
    FieldSchema(
        name="created_at",
        dtype=DataType.INT64
    )
]

# 스키마 생성
schema = CollectionSchema(
    fields=fields,
    description="상품 검색 컬렉션",
    enable_dynamic_field=True  # 동적 필드 활성화
)
```

### 지원 데이터 타입

#### 스칼라 타입

| 타입 | 설명 | 범위/크기 |
|------|------|----------|
| `INT8` | 8비트 정수 | -128 ~ 127 |
| `INT16` | 16비트 정수 | -32,768 ~ 32,767 |
| `INT32` | 32비트 정수 | -2^31 ~ 2^31-1 |
| `INT64` | 64비트 정수 | -2^63 ~ 2^63-1 |
| `FLOAT` | 32비트 부동소수점 | - |
| `DOUBLE` | 64비트 부동소수점 | - |
| `BOOL` | 불리언 | true/false |
| `VARCHAR` | 가변 문자열 | max_length 지정 필요 |
| `JSON` | JSON 객체 | - |
| `ARRAY` | 배열 | element_type 지정 필요 |

#### 벡터 타입

| 타입 | 설명 | 사용 사례 |
|------|------|----------|
| `FLOAT_VECTOR` | 32비트 부동소수점 벡터 | 일반적인 임베딩 |
| `BINARY_VECTOR` | 바이너리 벡터 | 해시 기반 검색 |
| `FLOAT16_VECTOR` | 16비트 부동소수점 벡터 | 메모리 절약 |
| `BFLOAT16_VECTOR` | BFloat16 벡터 | ML 모델 출력 |
| `SPARSE_FLOAT_VECTOR` | 희소 벡터 | BM25, SPLADE 등 |

### 동적 필드

스키마에 정의되지 않은 필드를 런타임에 추가할 수 있습니다.

```python
# 스키마에 동적 필드 활성화
schema = CollectionSchema(
    fields=fields,
    enable_dynamic_field=True
)

# 데이터 삽입 시 추가 필드 포함
data = [
    {
        "id": 1,
        "embedding": [0.1, 0.2, ...],
        "title": "상품명",
        # 동적 필드
        "custom_field_1": "값1",
        "custom_field_2": 123
    }
]
```

**주의사항:**
- 동적 필드는 인덱스 생성 불가
- 필터링은 가능하지만 성능이 낮을 수 있음
- JSON 타입처럼 동작

---

## 인덱스 종류

### 벡터 인덱스

#### 1. FLAT

**설명:**
- 선형 검색 (Brute Force)
- 인덱스 없이 모든 벡터와 비교
- 정확도 100%

**특징:**
- 가장 정확한 결과
- 작은 데이터셋에 적합 (< 100만 개)
- 빌드 시간 없음
- 메모리 사용량 높음

**파라미터:**
```python
index_params = {
    "metric_type": "L2",  # or "IP", "COSINE"
    "index_type": "FLAT"
}
```

**사용 사례:**
- 소규모 데이터셋
- 최고 정확도 필요
- 기준(baseline) 비교

#### 2. IVF_FLAT

**설명:**
- Inverted File Index
- 데이터를 클러스터로 나누고 가까운 클러스터만 검색
- 근사 검색

**특징:**
- FLAT보다 빠름
- 정확도 조절 가능
- 메모리 효율적

**파라미터:**
```python
index_params = {
    "metric_type": "L2",
    "index_type": "IVF_FLAT",
    "params": {
        "nlist": 1024  # 클러스터 수
    }
}

search_params = {
    "metric_type": "L2",
    "params": {
        "nprobe": 10  # 검색할 클러스터 수
    }
}
```

**튜닝 가이드:**
- `nlist`: 데이터 크기의 √(n) ~ 4√(n)
- `nprobe`: 1 ~ nlist
- nprobe ↑ → 정확도 ↑, 속도 ↓

**사용 사례:**
- 중간 규모 데이터셋 (100만 ~ 1000만)
- 속도와 정확도 균형

#### 3. IVF_SQ8

**설명:**
- IVF_FLAT + Scalar Quantization
- 벡터를 8비트로 압축

**특징:**
- 메모리 사용량 75% 감소
- 정확도 약간 감소
- 속도는 IVF_FLAT과 유사

**파라미터:**
```python
index_params = {
    "metric_type": "L2",
    "index_type": "IVF_SQ8",
    "params": {
        "nlist": 1024
    }
}
```

**사용 사례:**
- 메모리 제약이 있는 환경
- 약간의 정확도 손실 허용

#### 4. IVF_PQ

**설명:**
- IVF + Product Quantization
- 벡터를 서브벡터로 나누고 각각 양자화
- 최대 압축

**특징:**
- 메모리 사용량 95% 감소
- 정확도 손실 있음
- 대규모 데이터셋에 적합

**파라미터:**
```python
index_params = {
    "metric_type": "L2",
    "index_type": "IVF_PQ",
    "params": {
        "nlist": 1024,
        "m": 8,      # 서브벡터 수 (dim의 약수여야 함)
        "nbits": 8   # 비트 수 (4 or 8)
    }
}
```

**튜닝 가이드:**
- `m`: 벡터 차원의 약수, 보통 dim/8 ~ dim/16
- `m` ↑ → 정확도 ↑, 메모리 ↑
- `nbits`: 8 권장

**사용 사례:**
- 대규모 데이터셋 (1억 개 이상)
- 메모리가 매우 제한적인 환경

#### 5. HNSW

**설명:**
- Hierarchical Navigable Small World
- 그래프 기반 인덱스
- 계층적 구조

**특징:**
- 높은 정확도
- 빠른 검색 속도
- 메모리 사용량 높음
- 삽입 속도 느림

**파라미터:**
```python
index_params = {
    "metric_type": "L2",
    "index_type": "HNSW",
    "params": {
        "M": 16,           # 연결 수
        "efConstruction": 200  # 빌드 시 탐색 깊이
    }
}

search_params = {
    "metric_type": "L2",
    "params": {
        "ef": 100  # 검색 시 탐색 깊이
    }
}
```

**튜닝 가이드:**
- `M`: 4 ~ 64, 기본값 16
  - M ↑ → 정확도 ↑, 메모리 ↑, 빌드 시간 ↑
- `efConstruction`: 100 ~ 500
  - efConstruction ↑ → 정확도 ↑, 빌드 시간 ↑
- `ef`: efConstruction 이상
  - ef ↑ → 정확도 ↑, 검색 시간 ↑

**사용 사례:**
- 높은 정확도 필요
- 메모리 여유 있음
- 실시간 검색 중요

#### 6. SCANN

**설명:**
- Scalable Nearest Neighbors
- Google 개발
- 양자화 + 그래프 결합

**특징:**
- 균형잡힌 성능
- 메모리 효율적
- 높은 정확도

**파라미터:**
```python
index_params = {
    "metric_type": "L2",
    "index_type": "SCANN",
    "params": {
        "nlist": 1024,
        "with_raw_data": True
    }
}
```

**사용 사례:**
- 대규모 데이터셋
- 정확도와 성능 모두 중요

#### 7. GPU 인덱스

Milvus는 GPU 가속 인덱스를 지원합니다.

**종류:**
- `GPU_IVF_FLAT`
- `GPU_IVF_PQ`
- `GPU_CAGRA` (NVIDIA Rapids)

**특징:**
- 10~100배 빠른 검색
- GPU 메모리 필요
- 대규모 배치 검색에 유리

```python
index_params = {
    "metric_type": "L2",
    "index_type": "GPU_IVF_FLAT",
    "params": {
        "nlist": 1024
    }
}
```

### 스칼라 인덱스

스칼라 필드에도 인덱스를 생성하여 필터링 성능을 향상시킬 수 있습니다.

```python
# 스칼라 필드 인덱스 생성
collection.create_index(
    field_name="category",
    index_name="category_index"
)

collection.create_index(
    field_name="price",
    index_name="price_index"
)
```

**지원 타입:**
- INT, FLOAT, VARCHAR
- ARRAY, JSON (자동 인덱스)

### 거리 메트릭

| 메트릭 | 설명 | 범위 | 사용 사례 |
|--------|------|------|----------|
| `L2` | 유클리디안 거리 | [0, ∞) | 일반적인 벡터 |
| `IP` | 내적 (Inner Product) | (-∞, ∞) | 정규화된 벡터 |
| `COSINE` | 코사인 유사도 | [-1, 1] | 방향 중요 |
| `HAMMING` | 해밍 거리 | [0, dim] | 바이너리 벡터 |
| `JACCARD` | 자카드 거리 | [0, 1] | 바이너리 벡터 |

**선택 가이드:**
- 정규화된 벡터 → `IP` (가장 빠름)
- 일반 벡터 → `L2`
- 방향만 중요 → `COSINE`
- 바이너리 → `HAMMING`

### 인덱스 선택 가이드

```
데이터 크기 < 100만
    └─→ FLAT (정확도 100%)

데이터 크기 100만 ~ 1000만
    ├─→ 정확도 우선: HNSW
    └─→ 속도 우선: IVF_FLAT

데이터 크기 1000만 ~ 1억
    ├─→ 메모리 충분: HNSW
    ├─→ 메모리 제한: IVF_SQ8
    └─→ 균형: SCANN

데이터 크기 > 1억
    ├─→ 메모리 매우 제한: IVF_PQ
    ├─→ GPU 있음: GPU_IVF_FLAT
    └─→ 균형: SCANN

실시간 삽입 많음
    └─→ HNSW (동적 업데이트 우수)

정확도 100% 필요
    └─→ FLAT (또는 높은 파라미터의 HNSW)
```

---

## 설치 및 배포

### 1. Docker Compose (개발/테스트)

가장 간단한 설치 방법입니다.

```bash
# Milvus 다운로드
wget https://github.com/milvus-io/milvus/releases/download/v2.4.0/milvus-standalone-docker-compose.yml -O docker-compose.yml

# 실행
docker-compose up -d

# 확인
docker-compose ps
```

**구성 요소:**
- Milvus (단일 컨테이너)
- etcd
- MinIO

**접속:**
- Milvus: `localhost:19530`
- MinIO Console: `localhost:9001`

### 2. Kubernetes (프로덕션)

Helm Chart를 사용한 배포입니다.

```bash
# Helm 레포지토리 추가
helm repo add milvus https://zilliztech.github.io/milvus-helm/
helm repo update

# 설치
helm install milvus milvus/milvus \
  --set cluster.enabled=true \
  --set pulsar.enabled=true \
  --set kafka.enabled=false

# 확인
kubectl get pods -l app.kubernetes.io/instance=milvus
```

**최소 리소스 권장:**
- Query Node: 2 CPU, 4GB RAM
- Data Node: 2 CPU, 4GB RAM
- Index Node: 4 CPU, 8GB RAM

### 3. Milvus Lite (임베디드)

Python 프로세스 내에서 실행되는 경량 버전입니다.

```bash
pip install milvus
```

```python
from milvus import default_server

# 서버 시작
default_server.start()

# 연결
from pymilvus import connections
connections.connect(host='127.0.0.1', port=default_server.listen_port)

# 종료 시
default_server.stop()
```

**특징:**
- 설치 불필요
- 프로토타이핑에 적합
- 프로덕션 부적합

### 4. Milvus Operator (고급)

Kubernetes Operator를 통한 관리입니다.

```bash
# Operator 설치
kubectl apply -f https://raw.githubusercontent.com/milvus-io/milvus-operator/main/deploy/manifests/deployment.yaml

# Milvus 클러스터 생성
kubectl apply -f milvus-cluster.yaml
```

**장점:**
- 선언적 관리
- 자동 스케일링
- 롤링 업데이트

### 배포 모드 비교

| 모드 | 사용 사례 | 확장성 | 고가용성 |
|------|----------|--------|----------|
| Docker Compose | 개발, 테스트 | ✗ | ✗ |
| Milvus Lite | 프로토타입, 로컬 | ✗ | ✗ |
| Kubernetes | 프로덕션 | ✓ | ✓ |
| Operator | 대규모 프로덕션 | ✓✓ | ✓✓ |

### 설정 최적화

#### milvus.yaml 주요 설정

```yaml
# 기본 설정
common:
  dataCoordTimeTick: 2s
  
# 세그먼트 설정
dataCoord:
  segment:
    maxSize: 512  # MB
    sealProportion: 0.75
    
# 쿼리 노드 설정
queryNode:
  cache:
    enabled: true
    memoryLimit: 8589934592  # 8GB
  
  # 검색 동시성
  searchParallelism: 4
  
# 인덱스 노드 설정
indexNode:
  scheduler:
    buildParallel: 2
    
# 프록시 설정
proxy:
  maxTaskNum: 1024
  
# 리소스 제한
queryCoord:
  balanceCheckInterval: 60s
  overloadedMemoryThresholdPercentage: 90
```

---

## 기본 사용법

### Python SDK 설치

```bash
pip install pymilvus
```

### 1. 연결

```python
from pymilvus import connections

# 연결
connections.connect(
    alias="default",
    host='localhost',
    port='19530',
    user='username',     # 인증이 활성화된 경우
    password='password'
)

# 연결 확인
print(connections.list_connections())
```

### 2. 컬렉션 생성

```python
from pymilvus import CollectionSchema, FieldSchema, DataType, Collection

# 스키마 정의
fields = [
    FieldSchema(name="id", dtype=DataType.INT64, is_primary=True, auto_id=False),
    FieldSchema(name="embedding", dtype=DataType.FLOAT_VECTOR, dim=128),
    FieldSchema(name="text", dtype=DataType.VARCHAR, max_length=1000),
    FieldSchema(name="category", dtype=DataType.VARCHAR, max_length=100)
]

schema = CollectionSchema(
    fields=fields,
    description="문서 검색 컬렉션"
)

# 컬렉션 생성
collection = Collection(
    name="documents",
    schema=schema
)

print(f"컬렉션 생성됨: {collection.name}")
```

### 3. 인덱스 생성

```python
# 벡터 인덱스
index_params = {
    "metric_type": "L2",
    "index_type": "IVF_FLAT",
    "params": {"nlist": 1024}
}

collection.create_index(
    field_name="embedding",
    index_params=index_params,
    index_name="embedding_index"
)

# 스칼라 인덱스
collection.create_index(
    field_name="category",
    index_name="category_index"
)

print("인덱스 생성 완료")
```

### 4. 데이터 삽입

```python
import numpy as np

# 데이터 준비
data = [
    [1, 2, 3, 4, 5],  # id
    [  # embedding
        np.random.rand(128).tolist(),
        np.random.rand(128).tolist(),
        np.random.rand(128).tolist(),
        np.random.rand(128).tolist(),
        np.random.rand(128).tolist()
    ],
    ["텍스트 1", "텍스트 2", "텍스트 3", "텍스트 4", "텍스트 5"],  # text
    ["A", "B", "A", "C", "B"]  # category
]

# 삽입
mr = collection.insert(data)
print(f"삽입된 엔티티 수: {mr.insert_count}")

# Flush (선택사항, 자동으로도 수행됨)
collection.flush()
```

### 5. 데이터 로드

검색하기 전에 컬렉션을 메모리에 로드해야 합니다.

```python
# 전체 컬렉션 로드
collection.load()

# 특정 파티션만 로드
# collection.load(partition_names=["partition_1"])

print("컬렉션 로드 완료")
```

### 6. 검색 (Search)

```python
# 검색 쿼리
search_params = {
    "metric_type": "L2",
    "params": {"nprobe": 10}
}

query_vector = np.random.rand(128).tolist()

results = collection.search(
    data=[query_vector],
    anns_field="embedding",
    param=search_params,
    limit=10,  # Top-K
    output_fields=["text", "category"],  # 반환할 필드
    expr="category == 'A'"  # 필터 (선택사항)
)

# 결과 출력
for hits in results:
    for hit in hits:
        print(f"ID: {hit.id}, Distance: {hit.distance}, Text: {hit.entity.get('text')}")
```

### 7. 쿼리 (Query)

스칼라 필드로 데이터 조회입니다.

```python
# ID로 조회
results = collection.query(
    expr="id in [1, 2, 3]",
    output_fields=["text", "category"]
)

# 조건으로 조회
results = collection.query(
    expr="category == 'A' and id > 1",
    output_fields=["id", "text"],
    limit=10
)

print(results)
```

### 8. 삭제

```python
# ID로 삭제
collection.delete(expr="id in [1, 2, 3]")

# 조건으로 삭제
collection.delete(expr="category == 'B'")

print("삭제 완료")
```

### 9. 업데이트

Milvus는 직접 업데이트를 지원하지 않습니다. 삭제 후 재삽입해야 합니다.

```python
# 1. 삭제
collection.delete(expr="id == 1")

# 2. 새 데이터 삽입
new_data = [
    [1],
    [np.random.rand(128).tolist()],
    ["업데이트된 텍스트"],
    ["A"]
]
collection.insert(new_data)
```

### 10. 컬렉션 관리

```python
# 컬렉션 언로드
collection.release()

# 컬렉션 삭제
collection.drop()

# 모든 컬렉션 조회
from pymilvus import utility
collections = utility.list_collections()
print(collections)

# 컬렉션 통계
stats = collection.get_stats()
print(stats)
```

---

## 고급 기능

### 1. 파티션 관리

```python
# 파티션 생성
partition_a = collection.create_partition("partition_A")
partition_b = collection.create_partition("partition_B")

# 파티션에 데이터 삽입
partition_a.insert(data_a)
partition_b.insert(data_b)

# 특정 파티션에서만 검색
results = collection.search(
    data=[query_vector],
    anns_field="embedding",
    param=search_params,
    limit=10,
    partition_names=["partition_A"]  # 특정 파티션만 검색
)

# 파티션 로드/언로드
partition_a.load()
partition_a.release()

# 파티션 삭제
partition_a.drop()
```

**파티션 활용 패턴:**

```python
# 날짜별 파티션
collection.create_partition("2024_01")
collection.create_partition("2024_02")

# 최근 데이터만 검색
results = collection.search(
    data=[query_vector],
    anns_field="embedding",
    param=search_params,
    limit=10,
    partition_names=["2024_02"]  # 최근 달만 검색
)
```

### 2. 하이브리드 검색

벡터 검색 + 스칼라 필터링을 결합합니다.

```python
# 가격 범위 + 카테고리 필터
results = collection.search(
    data=[query_vector],
    anns_field="embedding",
    param=search_params,
    limit=10,
    expr="price >= 100 and price <= 500 and category == '전자기기'"
)

# IN 연산자
results = collection.search(
    data=[query_vector],
    anns_field="embedding",
    param=search_params,
    limit=10,
    expr="category in ['A', 'B', 'C']"
)

# LIKE 연산자
results = collection.search(
    data=[query_vector],
    anns_field="embedding",
    param=search_params,
    limit=10,
    expr="title like '%keyword%'"
)
```

**지원 연산자:**
- 비교: `==`, `!=`, `>`, `<`, `>=`, `<=`
- 논리: `and`, `or`, `not`
- 멤버십: `in`, `not in`
- 패턴: `like` (VARCHAR에만)
- 범위: `between`

### 3. 다중 벡터 검색

한 컬렉션에 여러 벡터 필드를 가질 수 있습니다.

```python
# 스키마에 여러 벡터 필드 정의
fields = [
    FieldSchema(name="id", dtype=DataType.INT64, is_primary=True),
    FieldSchema(name="image_embedding", dtype=DataType.FLOAT_VECTOR, dim=512),
    FieldSchema(name="text_embedding", dtype=DataType.FLOAT_VECTOR, dim=768),
    FieldSchema(name="title", dtype=DataType.VARCHAR, max_length=500)
]

# 각 벡터 필드에 인덱스 생성
collection.create_index("image_embedding", index_params)
collection.create_index("text_embedding", index_params)

# 이미지 벡터로 검색
results_image = collection.search(
    data=[image_query_vector],
    anns_field="image_embedding",
    param=search_params,
    limit=10
)

# 텍스트 벡터로 검색
results_text = collection.search(
    data=[text_query_vector],
    anns_field="text_embedding",
    param=search_params,
    limit=10
)
```

### 4. 배치 검색

여러 쿼리를 한 번에 처리합니다.

```python
# 여러 쿼리 벡터
query_vectors = [
    np.random.rand(128).tolist(),
    np.random.rand(128).tolist(),
    np.random.rand(128).tolist()
]

# 배치 검색
results = collection.search(
    data=query_vectors,
    anns_field="embedding",
    param=search_params,
    limit=10
)

# 결과는 쿼리 수만큼 반환됨
for idx, hits in enumerate(results):
    print(f"Query {idx}:")
    for hit in hits:
        print(f"  ID: {hit.id}, Distance: {hit.distance}")
```

### 5. 범위 검색 (Range Search)

거리 임계값 내의 모든 벡터를 찾습니다.

```python
search_params = {
    "metric_type": "L2",
    "params": {
        "nprobe": 10,
        "radius": 10.0,      # 최대 거리
        "range_filter": 0.0  # 최소 거리 (선택)
    }
}

results = collection.search(
    data=[query_vector],
    anns_field="embedding",
    param=search_params,
    limit=100  # 최대 반환 개수
)
```

### 6. Iterator (검색 반복자)

대량의 결과를 페이지네이션하여 가져옵니다.

```python
from pymilvus import SearchIterator

# 검색 iterator 생성
iterator = SearchIterator(
    collection=collection,
    data=[query_vector],
    anns_field="embedding",
    param=search_params,
    batch_size=100,  # 배치 크기
    output_fields=["text"]
)

# 결과 반복
while True:
    results = iterator.next()
    if not results:
        break
    
    for hit in results:
        print(f"ID: {hit.id}, Distance: {hit.distance}")

iterator.close()
```

### 7. 쿼리 Iterator

쿼리 결과를 페이지네이션합니다.

```python
from pymilvus import QueryIterator

iterator = QueryIterator(
    collection=collection,
    expr="category == 'A'",
    output_fields=["id", "text"],
    batch_size=1000
)

while True:
    results = iterator.next()
    if not results:
        break
    
    for entity in results:
        print(entity)

iterator.close()
```

### 8. 타임 트래블 (Time Travel)

특정 시점의 데이터를 조회합니다.

```python
import time

# 현재 시간 저장
timestamp = int(time.time() * 1000)

# 데이터 삽입
collection.insert(data)

# 시간 여행 검색 (삽입 전 상태)
results = collection.search(
    data=[query_vector],
    anns_field="embedding",
    param=search_params,
    limit=10,
    travel_timestamp=timestamp  # 특정 시점
)
```

**보관 기간:**
- 기본값: 24시간
- 설정: `common.retentionDuration` (milvus.yaml)

### 9. 일관성 수준 설정

```python
from pymilvus import ConsistencyLevel

# Strong Consistency (최신 데이터 보장)
results = collection.search(
    data=[query_vector],
    anns_field="embedding",
    param=search_params,
    limit=10,
    consistency_level=ConsistencyLevel.Strong
)

# Eventually Consistency (최고 성능)
results = collection.search(
    data=[query_vector],
    anns_field="embedding",
    param=search_params,
    limit=10,
    consistency_level=ConsistencyLevel.Eventually
)
```

**수준별 특징:**
- **Strong**: 가장 느림, 항상 최신 데이터
- **Bounded**: 지정된 시간 내 데이터
- **Session**: 세션 내 일관성
- **Eventually**: 가장 빠름, 최종 일관성

### 10. 벌크 삽입 (Bulk Insert)

대량 데이터를 효율적으로 삽입합니다.

```python
# 파일에서 벌크 삽입 (JSON, CSV, Parquet 지원)
task_id = utility.do_bulk_insert(
    collection_name="documents",
    files=["data1.json", "data2.json"]
)

# 진행 상황 확인
task = utility.get_bulk_insert_state(task_id=task_id)
print(f"Progress: {task.progress}%")
print(f"State: {task.state}")
```

### 11. 컴팩션 (Compaction)

작은 세그먼트를 병합하여 성능을 향상시킵니다.

```python
# 수동 컴팩션
collection.compact()

# 컴팩션 진행 상황 확인
while collection.get_compaction_state() == "InProgress":
    time.sleep(1)

print("Compaction completed")
```

### 12. 인덱스 재빌드

인덱스를 삭제하고 재생성합니다.

```python
# 기존 인덱스 삭제
collection.drop_index(index_name="embedding_index")

# 새 인덱스 생성
new_index_params = {
    "metric_type": "L2",
    "index_type": "HNSW",
    "params": {"M": 32, "efConstruction": 200}
}

collection.create_index(
    field_name="embedding",
    index_params=new_index_params,
    index_name="embedding_index"
)
```

---

## 성능 최적화

### 1. 인덱스 선택

**데이터 크기별 권장 인덱스:**

```python
# 소규모 (< 100만)
index_params = {
    "metric_type": "L2",
    "index_type": "FLAT"
}

# 중규모 (100만 ~ 1000만)
index_params = {
    "metric_type": "L2",
    "index_type": "HNSW",
    "params": {"M": 16, "efConstruction": 200}
}

# 대규모 (> 1000만)
index_params = {
    "metric_type": "L2",
    "index_type": "IVF_PQ",
    "params": {"nlist": 2048, "m": 8, "nbits": 8}
}
```

### 2. 검색 파라미터 튜닝

```python
# 정확도 우선
search_params = {
    "metric_type": "L2",
    "params": {
        "nprobe": 128,  # 높은 값
        "ef": 200       # HNSW의 경우
    }
}

# 속도 우선
search_params = {
    "metric_type": "L2",
    "params": {
        "nprobe": 10,   # 낮은 값
        "ef": 64        # HNSW의 경우
    }
}

# 균형
search_params = {
    "metric_type": "L2",
    "params": {
        "nprobe": 32,
        "ef": 100
    }
}
```

### 3. 배치 처리

```python
# Bad: 반복문으로 개별 삽입
for data in dataset:
    collection.insert(data)

# Good: 배치로 삽입
batch_size = 1000
for i in range(0, len(dataset), batch_size):
    batch = dataset[i:i+batch_size]
    collection.insert(batch)
```

### 4. 파티션 활용

```python
# 시간 기반 파티션
collection.create_partition("2024_01")
collection.create_partition("2024_02")

# 최근 데이터만 검색 (속도 향상)
results = collection.search(
    data=[query_vector],
    anns_field="embedding",
    param=search_params,
    limit=10,
    partition_names=["2024_02"]  # 전체 데이터의 일부만 검색
)
```

### 5. 필터 최적화

```python
# Bad: 복잡한 OR 조건
expr = "category == 'A' or category == 'B' or category == 'C'"

# Good: IN 연산자 사용
expr = "category in ['A', 'B', 'C']"

# Bad: 범위를 개별 조건으로
expr = "price > 100 and price < 500"

# Good: BETWEEN 사용 (지원되는 경우)
expr = "price >= 100 and price <= 500"
```

### 6. output_fields 최소화

```python
# Bad: 모든 필드 가져오기
results = collection.search(
    data=[query_vector],
    anns_field="embedding",
    param=search_params,
    limit=10,
    output_fields=["*"]  # 모든 필드
)

# Good: 필요한 필드만
results = collection.search(
    data=[query_vector],
    anns_field="embedding",
    param=search_params,
    limit=10,
    output_fields=["id", "title"]  # 필요한 필드만
)
```

### 7. 캐싱 활용

```python
# 자주 사용하는 쿼리는 캐시됨
# 동일한 쿼리 반복 시 더 빠름
for _ in range(10):
    results = collection.search(
        data=[query_vector],  # 동일한 쿼리
        anns_field="embedding",
        param=search_params,
        limit=10
    )
```

### 8. 세그먼트 크기 조정

```yaml
# milvus.yaml
dataCoord:
  segment:
    maxSize: 1024  # 기본 512MB → 1GB로 증가
    sealProportion: 0.75
```

**가이드라인:**
- 대량 삽입 워크로드: 큰 세그먼트 (1GB+)
- 실시간 삽입: 작은 세그먼트 (256-512MB)

### 9. 메모리 관리

```yaml
# milvus.yaml
queryNode:
  cache:
    enabled: true
    memoryLimit: 17179869184  # 16GB
  
  # 메모리 부족 시 언로드
  enableMemoryEstimation: true
```

### 10. 연결 풀 사용

```python
from pymilvus import connections

# 연결 풀 설정
connections.connect(
    alias="default",
    host='localhost',
    port='19530',
    pool_size=10  # 연결 풀 크기
)
```

### 성능 벤치마크

**테스트 환경:**
- 데이터: 1백만 벡터 (dim=128)
- 하드웨어: 8 CPU, 16GB RAM
- 인덱스: IVF_FLAT (nlist=1024)

| 작업 | QPS | Latency |
|------|-----|---------|
| 삽입 (배치 1000) | 10,000 | 100ms |
| 검색 (top-10) | 1,000 | 10ms |
| 검색 + 필터 | 800 | 12ms |

**대규모 환경 (1억 벡터):**
- 인덱스: HNSW (M=16)
- QPS: 500-1000
- Latency: 20-50ms
- Recall@10: 95%+

---

## 모니터링 및 운영

### 1. 메트릭 수집

Milvus는 Prometheus 메트릭을 제공합니다.

```yaml
# milvus.yaml
metrics:
  enabled: true
  port: 9091
```

**주요 메트릭:**
- `milvus_proxy_req_count`: 요청 수
- `milvus_proxy_req_latency`: 요청 지연시간
- `milvus_querynode_search_latency`: 검색 지연시간
- `milvus_datanode_flush_buffer_op_count`: Flush 작업 수
- `milvus_indexnode_build_index_latency`: 인덱스 빌드 시간

### 2. Grafana 대시보드

```bash
# Prometheus 설정
prometheus:
  scrape_configs:
    - job_name: 'milvus'
      static_configs:
        - targets: ['milvus:9091']

# Grafana 대시보드 import
# Dashboard ID: 11963 (공식 Milvus 대시보드)
```

### 3. 로깅

```yaml
# milvus.yaml
log:
  level: info  # debug, info, warn, error
  file:
    rootPath: /var/lib/milvus/logs
    maxSize: 300  # MB
    maxAge: 10    # days
    maxBackups: 20
```

**로그 확인:**
```bash
# Docker
docker logs milvus-standalone

# Kubernetes
kubectl logs -f milvus-proxy-xxxxx
```

### 4. 헬스 체크

```python
from pymilvus import utility, connections

connections.connect(host='localhost', port='19530')

# 연결 상태 확인
if connections.has_connection("default"):
    print("Connected")

# 버전 확인
version = utility.get_server_version()
print(f"Milvus version: {version}")
```

**HTTP 엔드포인트:**
```bash
# 헬스 체크
curl http://localhost:9091/healthz

# 메트릭
curl http://localhost:9091/metrics
```

### 5. 백업 및 복구

```python
from pymilvus import utility

# 컬렉션 데이터 export
utility.export_collection(
    collection_name="documents",
    output_file="backup.json"
)

# 복구
utility.import_collection(
    collection_name="documents",
    input_file="backup.json"
)
```

**MinIO 백업:**
```bash
# MinIO 데이터 백업 (Object Storage)
mc mirror minio/milvus-bucket /backup/milvus-data

# etcd 백업 (메타데이터)
ETCDCTL_API=3 etcdctl snapshot save snapshot.db
```

### 6. 성능 프로파일링

```python
# 검색 성능 측정
import time

start = time.time()
results = collection.search(...)
end = time.time()

print(f"Search took {(end - start) * 1000:.2f}ms")

# 컬렉션 통계
stats = collection.get_stats()
print(stats)
```

### 7. 일반적인 문제 해결

**문제: OOM (Out of Memory)**
```yaml
# 해결: 메모리 제한 설정
queryNode:
  cache:
    memoryLimit: 8589934592  # 8GB
```

**문제: 느린 검색**
```python
# 해결 1: 인덱스 파라미터 조정
search_params = {
    "params": {"nprobe": 10}  # 값을 낮춤
}

# 해결 2: 파티션 활용
results = collection.search(
    ...,
    partition_names=["recent"]
)

# 해결 3: 필터 최적화
# Bad
expr = "price > 100 and price > 200 and price > 300"
# Good
expr = "price > 300"
```

**문제: 삽입 실패**
```python
# 원인: 세그먼트 Seal 대기
# 해결: 수동 Flush
collection.flush()
```

### 8. 스케일링

**수평 확장 (Kubernetes):**
```bash
# Query Node 스케일 아웃
kubectl scale deployment milvus-querynode --replicas=5

# Data Node 스케일 아웃
kubectl scale deployment milvus-datanode --replicas=3
```

**Auto Scaling:**
```yaml
# HPA 설정
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: milvus-querynode-hpa
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: milvus-querynode
  minReplicas: 2
  maxReplicas: 10
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
```

### 9. 보안

```yaml
# milvus.yaml
# 인증 활성화
common:
  security:
    authorizationEnabled: true

# TLS 활성화
tls:
  serverPemPath: /path/to/server.pem
  serverKeyPath: /path/to/server.key
  caPemPath: /path/to/ca.pem
```

```python
# 인증 연결
connections.connect(
    host='localhost',
    port='19530',
    user='username',
    password='password',
    secure=True  # TLS
)
```

### 10. 업그레이드

```bash
# Docker Compose
docker-compose pull
docker-compose up -d

# Kubernetes
helm upgrade milvus milvus/milvus --version 2.4.0

# 롤백 (필요시)
helm rollback milvus
```

**주의사항:**
- 메이저 버전 업그레이드 전 백업 필수
- 스키마 호환성 확인
- 테스트 환경에서 먼저 검증

---

## 요약

### Milvus를 선택해야 하는 경우

✅ **다음 경우에 Milvus 권장:**
- 대규모 벡터 데이터 (수천만 ~ 수억 개)
- 높은 QPS 요구사항 (1000+ QPS)
- 엔터프라이즈급 안정성 필요
- 복잡한 필터링 및 하이브리드 검색
- 프로덕션 환경
- 멀티 테넌시 필요
- 확장성 중요

❌ **다음 경우에 다른 솔루션 고려:**
- 소규모 프로토타입 (Chroma, Weaviate)
- 간단한 사용 사례
- 운영 리소스 부족
- 빠른 개발 우선

### 핵심 개념 정리

1. **Collection**: 테이블과 유사, 명시적 스키마
2. **Partition**: 데이터 분할, 검색 범위 축소
3. **Segment**: 최소 저장 단위 (Growing/Sealed)
4. **Index**: 검색 성능 향상 (FLAT, IVF, HNSW 등)
5. **Consistency Level**: 일관성과 성능 트레이드오프