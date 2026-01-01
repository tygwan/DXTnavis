0. 디버거에서 Autodesk.Navisworks.Api.dll에 대해 디컴파일한 코드를 표시합니다. 다른 옵션을 탐색하려면 '기타 옵션'을 클릭하세요. 라는 첫번째 오류가 출력됐어.

두번째 시도에서는 선택 트리에서 어떤 객체를 선택했는데 VS가,

internal static void Autodesk_002ENavisworks_002EInternal_002EApiImplementation_002E_003FA0x17eac9a8_002Edebug_break_cb()

{

	if (Autodesk_002ENavisworks_002EInternal_002EApiImplementation_002E_003FA0x17eac9a8_002Ef_has_managed_debugger)

	{

		Debugger.Break();

이 부분에서 멈추고 addin이 작동을 멈췄어.

중단된 디버거 부분에서 '계속'을 눌렀더니 navisworks가 멈추고 navisworks 오류보고서에 다음이 출력됐어.

'''

날짜/시간: 2025-10-11 17:10:40 +09:00

응용프로그램: Roamer.exe

오류: Access violation - code c0000005 (first/second chance not available)

충돌된 모듈 이름: mscorlib.dll

예외 주소: 0x00007ff92c2ebf64

예외 코드: c0000005

예외 플래그: 0

예외 매개변수: 0, 8

관리되는 예외 유형: System.AccessViolationException

관리되는 예외 주소: 0x00000210541d5258

'''