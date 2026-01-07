"""
기술개발보고서를 이미지 경로 수정 후 재생성
"""

from pathlib import Path
import re

# 경로 설정
brain_dir = Path(r"C:\Users\Yoon taegwan\.gemini\antigravity\brain\75e82786-b011-4ecc-9fa4-3069110aed2a")
images_dir = Path(r"c:\Users\Yoon taegwan\Desktop\AWP_2025\개발폴더\docs\images")

# Part 1 읽기
part1_file = brain_dir / "technical_report_part1.md"
part1 = part1_file.read_text(encoding='utf-8')

# 이미지 경로 매핑 (간단한 이름으로 변경)
image_mapping = {
    "revit_snapshot_initial_1763754792091.png": "01_revit_initial.png",
    "revit_snapshot_progress_1763754811706.png": "02_revit_progress.png",
    "revit_snapshot_complete_1763754833198.png": "03_revit_complete.png",
    "navisworks_main_ui_1763754905584.png": "04_navis_main.png",
    "navis_upload_ready_1763754931934.png": "05_navis_upload_ready.png",
    "navis_upload_progress_1763754957750.png": "06_navis_upload_progress.png"
}

# 이미지 파일 이름 변경 (복사본)
print("📸 이미지 파일 정리 중...")
for old_name, new_name in image_mapping.items():
    old_path = images_dir / old_name
    new_path = images_dir / new_name
    if old_path.exists():
        old_path.rename(new_path)
        print(f"   ✓ {old_name} → {new_name}")

# Part 1에 이미지 삽입
print("\n📝 보고서에 이미지 삽입 중...")

# Step 2 이미지
part1 = part1.replace(
    "![Revit 스냅샷 대화상자 - 초기 상태](file:///C:/Users/Yoon%20taegwan/.gemini/antigravity/brain/75e82786-b011-4ecc-9fa4-3069110aed2a/revit_snapshot_initial_1763754792091.png)",
    "![Revit 스냅샷 대화상자 - 초기 상태](file:///c:/Users/Yoon%20taegwan/Desktop/AWP_2025/개발폴더/docs/images/01_revit_initial.png)"
)

# Step 4 이미지
part1 = part1.replace(
    "![Revit 스냅샷 추출 중](file:///C:/Users/Yoon%20taegwan/.gemini/antigravity/brain/75e82786-b011-4ecc-9fa4-3069110aed2a/revit_snapshot_progress_1763754811706.png)",
    "![Revit 스냅샷 추출 중](file:///c:/Users/Yoon%20taegwan/Desktop/AWP_2025/개발폴더/docs/images/02_revit_progress.png)"
)

# Step 5 이미지
part1 = part1.replace(
    "![Revit 스냅샷 완료](file:///C:/Users/Yoon taegwan/.gemini/antigravity/brain/75e82786-b011-4ecc-9fa4-3069110aed2a/revit_snapshot_complete_1763754833198.png)",
    "![Revit 스냅샷 완료](file:///c:/Users/Yoon%20taegwan/Desktop/AWP_2025/개발폴더/docs/images/03_revit_complete.png)"
)

# Step 7 이미지
part1 = part1.replace(
    "![Navisworks 메인 UI](file:///C:/Users/Yoon%20taegwan/.gemini/antigravity/brain/75e82786-b011-4ecc-9fa4-3069110aed2a/navisworks_main_ui_1763754905584.png)",
    "![Navisworks 메인 UI](file:///c:/Users/Yoon%20taegwan/Desktop/AWP_2025/개발폴더/docs/images/04_navis_main.png)"
)

# Step 9 이미지
part1 = part1.replace(
    "![Navisworks 업로드 준비](file:///C:/Users/Yoon%20taegwan/.gemini/antigravity/brain/75e82786-b011-4ecc-9fa4-3069110aed2a/navis_upload_ready_1763754931934.png)",
    "![Navisworks 업로드 준비](file:///c:/Users/Yoon%20taegwan/Desktop/AWP_2025/개발폴더/docs/images/05_navis_upload_ready.png)"
)

# Step 10 이미지
part1 = part1.replace(
    "![Navisworks 업로드 진행](file:///C:/Users/Yoon%20taegwan/.gemini/antigravity/brain/75e82786-b011-4ecc-9fa4-3069110aed2a/navis_upload_progress_1763754957750.png)",
    "![Navisworks 업로드 진행](file:///c:/Users/Yoon%20taegwan/Desktop/AWP_2025/개발폴더/docs/images/06_navis_upload_progress.png)"
)

# Part 1 저장
part1_file.write_text(part1, encoding='utf-8')
print(f"   ✓ Part 1 이미지 경로 업데이트 완료")

# Part 2 읽기
part2_file = brain_dir / "technical_report_part2.md"
part2 = part2_file.read_text(encoding='utf-8')

# 결합
part2_content = '\n'.join(part2.split('\n')[2:])  # 첫 2줄 제거
combined = part1 + '\n\n' + part2_content

# 결합본 저장
combined_file = brain_dir / "technical_report_complete.md"
combined_file.write_text(combined, encoding='utf-8')

print(f"\n✅ 보고서 재생성 완료!")
print(f"   파일: {combined_file}")
print(f"   크기: {len(combined):,} bytes")
print(f"   이미지: 6개 삽입됨")
