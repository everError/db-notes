# Telegraf 기본 개념 및 사용 범위

Telegraf는 InfluxData에서 개발한 **경량의 에이전트형 데이터 수집 도구**로, 시스템, 애플리케이션, 센서 등 다양한 소스로부터 메트릭 데이터를 수집하여 InfluxDB를 포함한 다양한 출력 대상으로 전송할 수 있습니다.

---

## 1. Telegraf란?

> 다양한 환경의 데이터를 실시간으로 수집해 InfluxDB 등으로 전달하는 **플러그인 기반 에이전트**

* InfluxData의 TICK 스택 구성 요소 중 하나
* 수백 개의 입력(input), 출력(output) 플러그인 제공
* **에이전트 하나로 다양한 수집 대상 관리 가능**
* 로컬 또는 리모트 장치에서 실행 가능

---

## 2. 주요 구성 요소 (플러그인 기반 아키텍처)

| 유형                    | 역할          | 예시                                      |
| --------------------- | ----------- | --------------------------------------- |
| **Input Plugin**      | 데이터를 수집     | `cpu`, `mem`, `mqtt`, `modbus`, `opcua` |
| **Processor Plugin**  | 수집한 데이터를 가공 | `converter`, `regex`, `enum`            |
| **Aggregator Plugin** | 일정 시간 동안 집계 | `minmax`, `basicstats`                  |
| **Output Plugin**     | 수집한 데이터를 전송 | `influxdb`, `file`, `prometheus_client` |

> **입력 → 처리 → 집계 → 출력** 순서로 구성됨

---

## 3. 기본 작동 방식

1. `telegraf.conf` 파일을 기반으로 동작
2. 설정된 입력 플러그인이 데이터를 주기적으로 수집
3. 필요한 경우 Processor/Aggregator로 가공
4. 출력 플러그인을 통해 InfluxDB 등으로 전송

---

## 4. 일반적인 사용 예시

### 1) 서버 성능 모니터링

```toml
[[inputs.cpu]]
[[inputs.mem]]
[[outputs.influxdb]]
  urls = ["http://localhost:8086"]
  database = "telegraf"
```

* CPU/메모리 사용률 수집 → InfluxDB로 전송

### 2) IoT 센서 데이터 수집 (Modbus, OPC UA 등)

```toml
[[inputs.opcua]]
  endpoint = "opc.tcp://192.168.0.100:4840"
[[outputs.influxdb]]
```

* 산업용 장비에서 센서값 직접 수집 가능

### 3) 애플리케이션 로그/메트릭 수집

```toml
[[inputs.http]]
  urls = ["http://my-service/metrics"]
```

* 외부 서비스의 메트릭 엔드포인트를 수집

---

## 5. 장점

* **에이전트 방식**: 별도 서버 없이 동작 가능
* **가벼움**: Go로 작성되어 설치 및 실행이 간편함
* **확장성**: 다양한 입력/출력 플러그인으로 확장 가능
* **InfluxDB 외에도** Prometheus, Kafka, File 등 다양한 출력 지원

---

## 6. 참고

* [Telegraf 공식 문서](https://docs.influxdata.com/telegraf/)
* [Telegraf GitHub](https://github.com/influxdata/telegraf)
* [플러그인 목록](https://github.com/influxdata/telegraf/tree/release-1.30/plugins)

---

Telegraf는 InfluxDB 환경에서 **데이터 수집 자동화의 핵심 도구**로 사용되며, 다양한 데이터 수집 시나리오를 커버할 수 있도록 설계되었습니다. 이후에는 플러그인별 실습과 구성 예제로 확장해 나갈 수 있습니다.
