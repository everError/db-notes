# Database Study

다양한 데이터베이스 기술에 대한 학습과 실습 내용을 기록하는 레포지토리입니다.

---

## Relational

정형 데이터를 테이블 구조로 관리하는 전통적인 RDBMS입니다. SQL을 사용하며 ACID 트랜잭션을 보장합니다.

- [SQL Server](./relational/SQLServer/) — Microsoft의 엔터프라이즈 RDBMS
- [MySQL](./relational/MySQL/) — 웹 서비스에서 가장 널리 사용되는 오픈소스 RDBMS

---

## NoSQL

스키마가 유연하고 수평 확장에 강점을 가진 비관계형 데이터베이스입니다. 데이터 모델과 사용 목적에 따라 여러 종류로 나뉩니다.

### Wide-Column

행과 열로 구성된 테이블 구조지만 열을 동적으로 구성할 수 있습니다. 대규모 분산 환경에서 높은 쓰기 성능과 수평 확장성을 제공합니다.

- [Cassandra](./wide-column/Cassandra/) — Facebook이 개발한 마스터리스 분산 데이터베이스. 고가용성과 대규모 쓰기 처리에 강점

### Time-Series

시간의 흐름에 따라 발생하는 데이터를 효율적으로 저장하고 조회하는 데 특화되어 있습니다. IoT, 모니터링, 로그 분석 등에 주로 사용됩니다.

- [InfluxDB](./time-series/influxdb/) — 시계열 데이터 전용으로 설계된 고성능 데이터베이스. Retention Policy, 다운샘플링 기능 내장

### Vector

벡터 임베딩을 저장하고 유사도 기반 검색(ANN)에 특화되어 있습니다. AI/ML 모델과 연계한 시맨틱 검색, RAG 파이프라인 등에 활용됩니다.

- [ChromaDB](./vector/chroma/) — 경량 오픈소스 벡터 데이터베이스. 로컬 환경 및 소규모 프로젝트에 적합
- [Milvus](./vector/milvus/) — 대규모 벡터 데이터 처리에 특화된 분산 벡터 데이터베이스

### Multi-Model

하나의 데이터베이스 엔진에서 여러 데이터 모델(관계형, 문서, 그래프, 키-값 등)을 동시에 지원합니다.

- [SurrealDB](./multi-model/SurrealDB/) — SQL 문법을 지원하면서 문서, 그래프, 키-값 모델을 모두 아우르는 신생 멀티모델 데이터베이스
