export function quote(features: string) {
  return 25 + features.split(/\r?\n/).filter((line) => line.trim()).length * 2;
}
export function validateStep(
  step: number,
  values: Record<string, string>,
): { field: string; message: string } | null {
  if (step === 0) {
    for (const [field, label, min, max] of [
      ["discord", "Discord username", 2, 50],
      ["brandName", "Brand name", 2, 80],
      ["changes", "Your idea", 10, 1500],
    ] as const) {
      const value = (values[field] || "").trim();
      if (value.length < min || value.length > max)
        return { field, message: `${label}: use ${min}–${max} characters.` };
    }
  }
  if (step === 1) {
    for (const field of ["newFeatures", "removeFeatures"]) {
      const lines = (values[field] || "")
        .split(/\r?\n/)
        .map((s) => s.trim())
        .filter(Boolean);
      if (lines.length > 20 || lines.some((s) => s.length > 180))
        return {
          field,
          message: "Use up to 20 features, one per line (180 characters each).",
        };
    }
    if ((values.aesthetics || "").length > 1000)
      return {
        field: "aesthetics",
        message: "Keep style notes under 1,000 characters.",
      };
  }
  return null;
}
