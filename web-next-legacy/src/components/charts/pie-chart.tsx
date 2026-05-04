import type { ChartSegment } from "@/types/passport";

type SegmentWithPercentage = ChartSegment & { percentage?: number };

type PieChartProps = {
  title: string;
  segments: SegmentWithPercentage[];
  size?: number;
  legendColumns?: string;
};

const fallbackColors = ["#08a348", "#df6b3b", "#ead9a4", "#4f6f7d", "#a7a7a7", "#2f9ca3", "#d0a33a", "#313846"];

function polarPoint(center: number, radius: number, angle: number) {
  const radians = ((angle - 90) * Math.PI) / 180;
  return {
    x: center + radius * Math.cos(radians),
    y: center + radius * Math.sin(radians),
  };
}

function piePath(center: number, radius: number, startAngle: number, endAngle: number) {
  const start = polarPoint(center, radius, endAngle);
  const end = polarPoint(center, radius, startAngle);
  const largeArc = endAngle - startAngle > 180 ? 1 : 0;
  return [`M ${center} ${center}`, `L ${start.x} ${start.y}`, `A ${radius} ${radius} 0 ${largeArc} 0 ${end.x} ${end.y}`, "Z"].join(" ");
}

function percentage(segment: SegmentWithPercentage, total: number) {
  if (typeof segment.percentage === "number") return segment.percentage;
  return total > 0 ? Math.round((segment.value / total) * 1000) / 10 : 0;
}

export function PieChart({ title, segments, size = 260, legendColumns = "sm:grid-cols-2" }: PieChartProps) {
  const total = segments.reduce((sum, segment) => sum + segment.value, 0);
  const center = 110;
  const radius = 92;
  let currentAngle = 0;

  return (
    <figure className="space-y-4">
      <figcaption className="text-base font-semibold text-slate-500 dark:text-slate-300">{title}</figcaption>
      <svg width={size} height={size} viewBox="0 0 220 220" role="img" aria-label={title} className="mx-auto block">
        {total <= 0 ? (
          <circle cx={center} cy={center} r={radius} fill="#d8dee5" />
        ) : (
          segments.map((segment, index) => {
            const startAngle = currentAngle;
            const sweep = (segment.value / total) * 360;
            const endAngle = startAngle + sweep;
            currentAngle = endAngle;
            const mid = startAngle + sweep / 2;
            const labelPoint = polarPoint(center, radius * 0.72, mid);
            const pct = percentage(segment, total);
            const color = segment.color ?? fallbackColors[index % fallbackColors.length];
            return (
              <g key={`${segment.label}-${index}`}>
                <path d={piePath(center, radius, startAngle, endAngle)} fill={color} stroke="white" strokeWidth="1.5" />
                {pct >= 5 ? (
                  <text
                    x={labelPoint.x}
                    y={labelPoint.y}
                    textAnchor="middle"
                    dominantBaseline="middle"
                    className="fill-white text-[12px] font-semibold"
                    style={{ textShadow: "0 1px 2px rgba(0,0,0,0.45)" }}
                  >
                    {pct.toFixed(1)}%
                  </text>
                ) : null}
              </g>
            );
          })
        )}
      </svg>
      <ul className={`grid gap-x-4 gap-y-1 text-sm ${legendColumns}`}>
        {segments.map((segment, index) => (
          <li key={`${segment.label}-legend`} className="flex items-center gap-2">
            <span
              className="h-3 w-3 shrink-0 rounded-full"
              style={{ backgroundColor: segment.color ?? fallbackColors[index % fallbackColors.length] }}
            />
            <span style={{ color: segment.color ?? undefined }}>
              {segment.label}: {segment.value}
              {segment.unit ? segment.unit : ""}
            </span>
          </li>
        ))}
      </ul>
    </figure>
  );
}
