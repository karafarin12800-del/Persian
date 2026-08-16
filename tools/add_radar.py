from pathlib import Path

path = Path("app/src/main/java/com/persiawar2d/MainActivity.java")
s = path.read_text(encoding="utf-8")

marker = "drawWorld(c); drawHud(c); drawControls(c);"
if "drawRadar(c);" not in s:
    if marker not in s:
        raise SystemExit("Radar patch marker not found")
    s = s.replace(marker, "drawWorld(c); drawHud(c); drawRadar(c); drawControls(c);", 1)

method_marker = "\n        public void drawRadar(Canvas c) {"
if method_marker not in s:
    raise SystemExit("This simplified v11 source uses RadarOverlayView directly; no inline radar patch is required.")
path.write_text(s, encoding="utf-8")
print("Radar overlay patch checked")
