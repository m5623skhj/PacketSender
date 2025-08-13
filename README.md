# PacketSender

## 제작 기간
2025.08.02 ~ 진행 중

## 개요

`PacketSender`는 서버 개발자와 QA가 패킷을 손쉽게 송신할 수 있도록 지원하기 위해 만들고 있는 툴입니다.

### 주요 기능
1. **서버/클라이언트가 공유하는 프로토콜 파일(.yml)을 로드**
2. **GUI로 패킷 목록을 출력하고 선택**
3. **패킷 필드에 값 입력 UI 제공**
4. **사용자가 구성한 패킷을 서버로 송신 및 로그로 출력**
5. **서버에서 응답 받은 패킷을 로그로 출력**


예시:  
.yml 파일:   
<img width="726" height="606" alt="image" src="https://github.com/user-attachments/assets/5fed87e0-d3ae-40a8-9bf2-234f459f40ce" />

→ GUI 출력:
<img width="1178" height="738" alt="image" src="https://github.com/user-attachments/assets/de4940b8-e1c0-49f9-a330-e96f8682a8b2" />

- 송신한 패킷 및 수신 받은 패킷 로그 출력
  - UI 및 파일에 출력되며, 파일 로그는 실행 파일 위치/Logs 폴더에 출력됩니다.

---

## 왜 필요하다고 생각했는가?

- 서버 개발자가 예외 케이스를 재현하고 디버깅할 수 있음
- QA가 테스트 중 발생한 상황을 정확한 데이터 기반으로 재현 및 전달 가능
- 기존 클라이언트 환경을 셋업하거나 조작하는 부담을 줄일 수 있음

---

## 설계 시 고려사항

- 이 툴은 프로젝트와 독립적으로 유지되어야 함
  → 프로토콜 파일이 변경되어도 툴 자체를 재빌드할 필요 없이 동작해야 함

---

## 테스트 프로젝트

- 테스트를 위하여 [MultiSocketRUDP](https://github.com/m5623skhj/MultiSocketRUDP)를 사용하여 통신 테스트 중
- MultiSocketRUDP/ClientCore 라이브러리가 C++로 작성되어 있어서, Dll을 통해 프록시를 구성하고, C# 코드에서 해당 DLL의 함수를 호출하여 통신하도록 구성
- 위 내용은 ClientProxySender에서 구현 중

---

## TODO

- [✅] 패킷 직렬화/역직렬화 
- [✅] 송수신 로그 기능  
- [✅] 서버 응답 패킷 시각화  
