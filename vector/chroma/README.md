# ChromaDB

**ChromaDB**는 AI 네이티브 애플리케이션을 위한 오픈소스 벡터 데이터베이스입니다. 텍스트, 이미지, 오디오와 같은 비정형 데이터를 머신러닝 모델이 이해할 수 있는 숫자 벡터(임베딩) 형태로 저장하고, 이를 기반으로 \*\*유사도 검색(Similarity Search)\*\*을 빠르고 효율적으로 수행하는 데 특화되어 있습니다.

전통적인 데이터베이스가 정확한 값이나 키워드로 데이터를 찾는 것과 달리, ChromaDB는 데이터의 의미적 유사성을 기준으로 검색합니다. 예를 들어, "샌프란시스코의 유명한 다리"라는 텍스트 쿼리와 의미적으로 가장 유사한 이미지(골든 게이트 브릿지 사진)를 찾아낼 수 있습니다.

-----

## 주요 특징

  - **간단하고 빠른 시작**: `pip install chromadb` 명령어로 간단하게 설치할 수 있으며, 파이썬 및 자바스크립트/타입스크립트 SDK를 통해 쉽게 개발을 시작할 수 있습니다.
  - **풍부한 기능**: 벡터 검색뿐만 아니라 전체 텍스트 검색, 메타데이터 필터링, 문서 저장 등 검색 애플리케이션에 필요한 모든 기능을 갖추고 있습니다.
  - **통합성**: HuggingFace, OpenAI, Google 등 다양한 임베딩 모델과 기본적으로 통합되어 있으며, LangChain, LlamaIndex와 같은 LLM 프레임워크와도 쉽게 연동할 수 있습니다.
  - **오픈소스**: Apache 2.0 라이선스로 제공되어 누구나 자유롭게 사용하고 수정할 수 있습니다.
  - **확장성**: 로컬 환경에서는 DuckDB를, 대규모 애플리케이션을 위해서는 ClickHouse와 같은 스토리지 백엔드를 지원하여 확장성을 확보할 수 있습니다.
  - **인메모리 및 영구 저장소 지원**: 빠른 속도를 위해 인메모리 방식을 사용하면서도, 데이터 보존을 위해 영구 저장소 옵션도 제공합니다.

-----

## 작동 방식

1.  **컬렉션 생성 (Create Collection)**: 관계형 데이터베이스의 테이블과 유사한 '컬렉션'을 생성합니다. 컬렉션은 관련 있는 임베딩 그룹을 저장하고 관리하는 공간입니다.
2.  **데이터 추가 및 임베딩 (Add Data & Embedding)**: 텍스트 문서나 이미지 같은 원본 데이터와 함께 고유 ID, 메타데이터를 컬렉션에 추가합니다. 이때 ChromaDB는 내장된 임베딩 모델(기본값: `all-MiniLM-L6-v2`)을 사용하거나 사용자가 지정한 모델을 통해 데이터를 벡터로 변환(임베딩)하여 저장합니다.
3.  **인덱싱 (Indexing)**: 효율적인 검색을 위해 HNSW(Hierarchical Navigable Small World)와 같은 고급 인덱싱 기술을 사용하여 벡터 데이터를 구성합니다.
4.  **쿼리 및 검색 (Query & Search)**: 사용자가 텍스트나 벡터로 쿼리를 보내면, ChromaDB는 이를 벡터로 변환한 뒤 인덱스 내에서 코사인 유사도(Cosine Similarity)나 유클리드 거리(Euclidean Distance) 같은 척도를 사용하여 가장 유사한 벡터(데이터)를 찾아 반환합니다. 메타데이터를 이용한 필터링도 가능하여 특정 조건에 맞는 데이터만 검색할 수도 있습니다.

-----

## 주요 사용 사례

  - **검색 증강 생성 (RAG, Retrieval-Augmented Generation)**: 대규모 언어 모델(LLM)이 외부의 최신 정보나 특정 도메인 지식을 기반으로 더 정확하고 풍부한 답변을 생성하도록 돕습니다.
  - **의미론적 검색 (Semantic Search)**: 키워드 일치가 아닌 문맥과 의미를 기반으로 문서를 검색하여 더 관련성 높은 결과를 제공합니다.
  - **추천 시스템 (Recommender Systems)**: 사용자의 선호도나 아이템의 특징을 벡터로 표현하여 개인화된 콘텐츠, 상품 등을 추천합니다.
  - **이미지 검색 (Image Retrieval)**: 이미지의 특징 벡터를 저장하여 내용 기반으로 유사한 이미지를 검색합니다.
  - **이상 탐지 (Anomaly Detection)**: 정상 데이터의 벡터 분포를 학습한 후, 분포에서 벗어나는 이상 데이터를 탐지합니다.

-----

## 기본 사용법 (Python)

```python
import chromadb

# 1. 클라이언트 생성 (인메모리)
# 데이터를 영구적으로 저장하려면 경로를 지정합니다: chromadb.PersistentClient(path="/path/to/db")
client = chromadb.Client()

# 2. 컬렉션 생성 또는 가져오기
# get_or_create_collection은 컬렉션이 없으면 생성하고, 있으면 반환합니다.
collection = client.get_or_create_collection(name="my_collection")

# 3. 문서 추가
# 문서, 메타데이터, 고유 ID를 함께 추가합니다.
# ChromaDB가 자동으로 텍스트를 임베딩합니다.
collection.add(
    documents=[
        "This is a document about ChromaDB.",
        "ChromaDB is an open-source vector database.",
        "What is the capital of France?",
        "Paris is the capital and most populous city of France."
    ],
    metadatas=[
        {"source": "tech_doc"},
        {"source": "tech_doc"},
        {"source": "qa"},
        {"source": "qa"}
    ],
    ids=["doc1", "doc2", "qa1", "qa2"]
)

# 4. 쿼리 실행
# "vector databases"와 가장 관련성 높은 2개의 문서를 검색합니다.
results = collection.query(
    query_texts=["vector databases"],
    n_results=2
)

print(results)

# 5. 메타데이터를 이용한 필터링 쿼리
# source가 "qa"인 문서 중에서 "France"와 관련된 문서를 검색합니다.
results_with_filter = collection.query(
    query_texts=["France"],
    where={"source": "qa"},
    n_results=2
)

print(results_with_filter)
```