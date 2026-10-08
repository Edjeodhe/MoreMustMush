# -*- coding: utf-8 -*-
"""어떤 시뮬 기록·프리셋으로 맞출지. 환경변수로 바꾼다(기본 = tuned T2 기록).
   THEORY_PRESET = tuned | spec
   THEORY_DATA   = 기록 폴더 (balance_phase_s*.json · balance_state_s*.json · balance.log · price_table.json)
예) spec 초안 재현:  THEORY_PRESET=spec THEORY_DATA=BalanceData python model.py"""
import os

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))

# Defs.cs PRESETS (hp = 일반 버섯 체력, col0/colStep = 군락지 수 0레벨·레벨당)
PRESETS = {
    "tuned": dict(hp0=1.0, col0=12, colstep=5),
    "spec": dict(hp0=3.0, col0=3, colstep=3),
}
DEFAULT_DATA = {"tuned": os.path.join("BalanceData", "test", "before_1313"), "spec": os.path.join("BalanceData", "test", "before_1239")}  # spec 수렴 기록(12:39) 원본

PRESET = os.environ.get("THEORY_PRESET", "tuned")
DATA = os.path.join(ROOT, os.environ.get("THEORY_DATA", DEFAULT_DATA[PRESET]))
P = PRESETS[PRESET]
TAG = PRESET  # 결과 파일 꼬리표
