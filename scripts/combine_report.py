"""
기술개발보고서 Part 1 + Part 2를 하나의 파일로 결합
"""

from pathlib import Path

# 경로 설정
brain_dir = Path(r"C:\Users\Yoon taegwan\.gemini\antigravity\brain\75e82786-b011-4ecc-9fa4-3069110aed2a")
output_dir = Path(r"c:\Users\Yoon taegwan\Desktop\AWP_2025\개발폴더\docs\pdf")
output_dir.mkdir(parents=True, exist_ok=True)

# Part 1과 Part 2 읽기
part1 = (brain_dir / "technical_report_part1.md").read_text(encoding='utf-8')
part2 = (brain_dir / "technical_report_part2.md").read_text(encoding='utf-8')

# 결합 (Part 2 제목 제거하고 내용만)
# Part 2는 "# 기술개발보고서 Part 2 (계속)" 으로 시작하므로 첫 줄 제거
part2_content = '\n'.join(part2.split('\n')[2:])  # 첫 2줄 제거 (제목 + 빈 줄)

combined = part1 + '\n\n' + part2_content

# 결합된 파일 저장
combined_file = brain_dir / "technical_report_complete.md"
combined_file.write_text(combined, encoding='utf-8')

print(f"✅ 보고서 결합 완료: {combined_file}")
print(f"   총 크기: {len(combined):,} bytes")
