DX 통합 플랫폼: nD BIM 데이터 파이프라인 최종 아키텍처 프롬프트

역할: 당신은 Revit, Navisworks, PostgreSQL, FastAPI를 통합하여 엔터프라이즈급 nD BIM 데이터 플랫폼을 구축하는 수석 시스템 아키텍트입니다. 당신의 임무는 데이터의 생성, 기록, 가공, 제공, 활용 전 과정에 걸친 각 시스템의 역할과 상호작용을 명확히 정의하는 통합 데이터 파이프라인을 설계하는 것입니다.

최종 목표: 설계 변경 이력을 포함한 BIM 데이터를 중앙 데이터베이스에 축적하고, 이를 가공하여 분석용 인사이트를 생성하며, 최종적으로 Navisworks에서 자동화된 4D 시뮬레이션을 구축하고 Power BI에서 다차원 분석을 수행할 수 있는 완벽한 End-to-End 파이프라인을 구축한다.

통합 데이터 파이프라인: 5단계 상호작용

Phase 1: 데이터 생성 및 준비 (Source of Truth)

시스템: Autodesk Revit + DXrevit 애드인

역할: 모든 BIM 데이터의 '유일한 진실의 원천'으로서, 분석과 시뮬레이션에 필요한 모든 원시 데이터를 생성하고 준비한다.

상호작용 및 지침:

데이터 저작 (Authoring): BIM 모델링 단계에서, 모든 핵심 객체(Element)에 **'공정 ID (Activity ID)'**와 같은 사용자 정의 공유 매개변수를 부여하고, 시공 스케줄과 일치하는 값을 입력한다. cost, material 등 분석에 필요한 모든 속성을 표준화된 매개변수로 관리한다.

스냅샷 트리거: 설계 변경이 완료된 주요 마일스톤 시점에, 사용자는 DXrevit 애드인의 "DB에 스냅샷 저장" 버튼을 클릭한다.

데이터 패키징: DXrevit 애드인은 ModelVersion과 Timestamp를 생성하고, 모델의 모든 객체, 속성, 관계를 추출하여 API 전송에 최적화된 JSON 데이터 패키지를 생성한다.

Phase 2: 데이터 수집 및 원시 저장 (Ingestion & Raw Storage)

시스템: FastAPI 서버 (Ingestion Endpoint) + PostgreSQL DB (Raw Data Tables)

역할: Revit에서 생성된 데이터 스냅샷을 안전하게 수신하고, 가공되지 않은 원시 형태 그대로 '데이터 레이크'에 영구적으로 기록한다.

상호작용 및 지침:

데이터 수신: DXrevit 애드인은 생성된 JSON 패키지를 HttpClient를 통해 FastAPI 서버의 /ingest 엔드포인트에 POST 요청으로 전송한다.

데이터 검증 및 저장: FastAPI 서버는 수신된 데이터의 유효성을 검증한 후, Npgsql을 사용하여 PostgreSQL의 원시 데이터 테이블(metadata, objects, relationships)에 그대로 INSERT한다. 이 과정에서 기존 데이터는 절대 수정되거나 삭제되지 않고, 모든 버전이 시간 순서대로 누적된다.

Phase 3: 데이터 가공 및 분석용 뷰 생성 (Transformation)

시스템: PostgreSQL DB (Views & Functions)

역할: 원시 데이터 레이크 위에, 비즈니스 분석과 의사결정에 직접 사용할 수 있는 의미 있는 '정보'로 가공된 '데이터 웨어하우스' 계층을 구축한다.

상호작용 및 지침:

데이터 보강 (Enrichment): 데이터베이스 관리자는 material_unit_costs, task_durations 등 외부 데이터를 담은 별도의 테이블을 생성하고 관리한다.

분석용 뷰(View) 생성: DB 관리자는 원시 테이블과 보강 테이블을 JOIN하고, 복잡한 계산(SUM, COUNT, 차이 계산 등)을 수행하는 다양한 **SQL 뷰(View)**를 미리 생성해 둔다.

analytics_version_summary: 버전별 총 물량, 총 비용 등을 요약.

analytics_version_delta: 두 버전 간의 추가/삭제/수정된 객체 목록을 계산.

analytics_4d_link_data: Navisworks TimeLiner 자동화를 위해 필요한 '공정 ID'와 ObjectId를 매핑한 뷰.

Phase 4: 데이터 제공 (Serving)

시스템: FastAPI 서버 (Analytics Endpoints)

역할: 최종 사용자 애플리케이션(Navisworks, Power BI)이 복잡한 SQL 없이도 필요한 분석 데이터를 쉽게 가져갈 수 있도록, 잘 정의된 API를 제공한다.

상호작용 및 지침:

API 엔드포인트 구현: FastAPI는 PostgreSQL의 **분석용 뷰(Analytics Views)**를 조회하는 다양한 GET 엔드포인트를 구현한다.

/compare?v1=...&v2=...: analytics_version_delta 뷰를 조회하여 버전 비교 결과를 반환.

/summary/{version}: analytics_version_summary 뷰를 조회하여 특정 버전의 요약 정보를 반환.

/4d_link_data/{version}: analytics_4d_link_data 뷰를 조회하여 TimeLiner 자동 연결에 필요한 데이터를 반환.

Phase 5: 데이터 활용 및 시각화 (Utilization & Visualization)

시스템: Autodesk Navisworks + DXnavis 애드인, Power BI

역할: 제공된 데이터를 최종 사용자가 소비하여, 4D 시뮬레이션을 수행하고 다차원 분석을 통해 의사결정을 내린다.

상호작용 및 지침:

4D 시뮬레이션 자동화 (in Navisworks):

사용자는 DXnavis 애드인의 "TimeLiner 자동 구성" 버튼을 클릭한다.

DXnavis 애드인은 FastAPI의 /4d_link_data/{version} 엔드포인트를 호출하여 최신 버전의 '공정 ID - ObjectId' 매핑 데이터를 가져온다.

가져온 데이터를 규칙으로 사용하여, Navisworks Search API와 TimeLiner API를 통해 작업(Task)과 객체 세트(Selection Set)를 자동으로 연결한다.

다차원 분석 (in Power BI):

Power BI는 FastAPI의 분석용 엔드포인트(/summary, /compare 등)에 웹 데이터 소스로 연결하거나, PostgreSQL의 분석용 뷰에 직접 연결한다.

사용자는 이미 가공된 데이터를 사용하여, "버전별 비용 변화 추이", "공종별 물량 비교" 등 고차원의 분석 대시보드를 손쉽게 구성하고 인사이트를 도출한다.

이 통합 파이프라인은 각 시스템의 강점을 극대화하고 약점은 보완하는 현대적인 데이터 아키텍처입니다. Revit은 데이터의 정확성을, PostgreSQL은 데이터의 역사와 가공을, FastAPI는 데이터의 안전한 제공을, 그리고 Navisworks와 Power BI는 데이터의 최종적인 활용을 책임집니다.