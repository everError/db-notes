# InfluxDB 3 Core 실습 가이드 (Docker Compose + 인증)

이 문서는 InfluxDB 3 Core를 Docker Compose 환경에서 실행하고, 인증 토큰을 발급하여 보안된 방식으로 API를 호출하는 실습 절차를 정리한 가이드입니다.

---

## 📦 1. Docker Compose 구성 (인증 활성화)

```yaml
services:
  influxdb:                           # 서비스 이름 (컨테이너 이름과 별개)
    image: influxdb:3-core            # 사용할 공식 이미지 (InfluxDB 3 Core)
    container_name: influxdb3         # 생성될 컨테이너의 이름 지정
    ports:
      - 8181:8181                    # 호스트:컨테이너 포트 바인딩 (InfluxDB API 포트)
    volumes:
      - influxdb_data:/var/lib/influxdb3  # 볼륨 마운트 (데이터 영속성 확보)
    networks:
      - influxdb                     # 사용할 사용자 정의 네트워크
    command:                         # 컨테이너 시작 시 실행할 커맨드 지정
      - influxdb3
      - serve
      - --node-id=node0              # 노드 ID (클러스터에서 고유 식별자)
      - --object-store=file          # 로컬 파일 기반 저장소 사용
      - --data-dir=/var/lib/influxdb3 # 실제 데이터 파일 경로 (볼륨과 일치해야 함)
      - --log-filter=info            # 로그 레벨 설정

networks:
  influxdb:
    driver: bridge                   # 기본 브리지 네트워크 드라이버 사용

volumes:
  influxdb_data:                     # 데이터 저장용 볼륨 정의
```

> ❗ 인증 기능 비활성화 `--without-auth`

---

## 🔐 2. 인증 토큰 발급 절차

### ✅ 컨테이너 실행 후, 내부 진입

```bash
docker compose up -d --force-recreate
```

```bash
docker compose exec influxdb /bin/sh
```

### ✅ 토큰 생성

```bash
docker compose exec influxdb influxdb3 create token --admin
```

```bash
influxdb3 create token --admin
```

* 관리자 권한 토큰이 출력됩니다 (plain 문자열)
* 반드시 복사하여 저장하세요. 다시 조회할 수 없습니다.

---

## 🧪 토큰 포함 API 요청 테스트

```bash
curl -H "Authorization: Bearer <복사한_토큰>" http://localhost:8181/health
```