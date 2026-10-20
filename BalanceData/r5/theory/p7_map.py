# P-7 맵: area = (1/zoom^2)^0.7 (Game.cs:253, 군락 수 배율) 과 실제 필드 면적 1/zoom^2 비교
import json, math, os
ED_COLS = [4, 6, 10, 17, 27]
def area(z): return (1 / (z * z)) ** 0.7
def cols(c, z): return max(1, round(c * area(z)))   # Game.cs:254 JsRound (0.5 올림, 여기선 근사)
old = [1.5, 1.35, 1.2, 1.1, 1.0]
z_formula = 1.5 * 0.85 ** (-1 / 1.4)    # 군락 배율식 기준 −15%
z_phys = 1.5 / math.sqrt(0.85)          # 실제 면적 −15%
z_lin = 1.5 / 0.85                      # 가로·세로 −15%
out = {"z_formula": z_formula, "z_phys": z_phys, "z_lin": z_lin, "rows": []}
for name, z in (("지금 Lv0", 1.5), ("안 1.68(군락식 −15%)", 1.68), ("안 1.63(면적 −15%)", 1.63), ("안 1.76(가로세로 −15%)", 1.76)):
    out["rows"].append({"name": name, "zoom": z, "field": (round(1920 / z), round(1080 / z)), "phys_area_vs_now": (1.5 / z) ** 2,
                        "col_area_vs_now": area(z) / area(1.5), "bar_max": round(1920 / z / 3), "cols_by_ed_cols": [cols(c, z) for c in ED_COLS],
                        "screen_scale_vs_now": z / 1.5})
new = [1.68, 1.5, 1.35, 1.2, 1.1, 1.0]
out["new_levels"] = [{"lv": i, "zoom": z, "field": (round(1920 / z), round(1080 / z)), "phys_vs_max": 1 / z ** 2, "cols_ed4": cols(27, z)} for i, z in enumerate(new)]
json.dump(out, open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "p7_map_out.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
for r in out["rows"]: print(r)
print("z formula/phys/lin", round(z_formula, 3), round(z_phys, 3), round(z_lin, 3))
for r in out["new_levels"]: print(r)
