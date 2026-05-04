import type { RecycledContentChart } from "@/types/passport";

type DonutChartProps = {
  chart: RecycledContentChart;
  size?: number;
};

const recycledColors = {
  pre: "#4f6f7d",
  post: "#08a348",
  primary: "#a7a7a7",
};

function polarPoint(center: number, radius: number, angle: number) {
  const radians = ((angle - 90) * Math.PI) / 180;
  return {
    x: center + radius * Math.cos(radians),
    y: center + radius * Math.sin(radians),
  };
}

function ringPath(center: number, outerRadius: number, innerRadius: number, startAngle: number, endAngle: number) {
  const outerStart = polarPoint(center, outerRadius, endAngle);
  const outerEnd = polarPoint(center, outerRadius, startAngle);
  const innerStart = polarPoint(center, innerRadius, startAngle);
  const innerEnd = polarPoint(center, innerRadius, endAngle);
  const largeArc = endAngle - startAngle > 180 ? 1 : 0;

  return [
    `M ${outerStart.x} ${outerStart.y}`,
    `A ${outerRadius} ${outerRadius} 0 ${largeArc} 0 ${outerEnd.x} ${outerEnd.y}`,
    `L ${innerStart.x} ${innerStart.y}`,
    `A ${innerRadius} ${innerRadius} 0 ${largeArc} 1 ${innerEnd.x} ${innerEnd.y}`,
    "Z",
  ].join(" ");
}

export function DonutChart({ chart, size = 220 }: DonutChartProps) {
  const segments = [
    { label: "Pre consumer share", value: chart.preConsumerShare, color: recycledColors.pre },
    { label: "Post consumer share", value: chart.postConsumerShare, color: recycledColors.post },
    { label: "Primary material", value: chart.primaryMaterialShare, color: recycledColors.primary },
  ];
  const total = segments.reduce((sum, segment) => sum + segment.value, 0);
  const center = 110;
  const outerRadius = 78;
  const innerRadius = 48;
  let currentAngle = 0;

  return (
    <figure className="space-y-3 text-center">
      <figcaption className="text-sm font-semibold uppercase text-slate-500 dark:text-slate-300">{chart.material}</figcaption>
      <svg width={size} height={size} viewBox="0 0 220 220" role="img" aria-label={`${chart.material} recycled content`}>
        {segments.map((segment) => {
          const startAngle = currentAngle;
          const sweep = total > 0 ? (segment.value / total) * 360 : 0;
          const endAngle = startAngle + sweep;
          currentAngle = endAngle;
          const mid = startAngle + sweep / 2;
          const labelPoint = polarPoint(center, (outerRadius + innerRadius) / 2, mid);
          return (
            <g key={segment.label}>
              <path d={ringPath(center, outerRadius, innerRadius, startAngle, endAngle)} fill={segment.color} stroke="white" strokeWidth="2" />
              {segment.value >= 5 ? (
                <text
                  x={labelPoint.x}
                  y={labelPoint.y}
                  textAnchor="middle"
                  dominantBaseline="middle"
                  className="fill-white text-[12px] font-semibold"
                  style={{ textShadow: "0 1px 2px rgba(0,0,0,0.45)" }}
                >
                  {segment.value.toFixed(1)}%
                </text>
              ) : null}
            </g>
          );
        })}
      </svg>
      <ul className="mx-auto flex max-w-xs flex-wrap justify-center gap-x-3 gap-y-1 text-sm">
        {segments.map((segment) => (
          <li key={`${chart.material}-${segment.label}`} className="flex items-center gap-1.5" style={{ color: segment.color }}>
            <span className="h-3 w-3 shrink-0 rounded-full" style={{ backgroundColor: segment.color }} />
            <span>
              {segment.label}: {segment.value}%
            </span>
          </li>
        ))}
      </ul>
    </figure>
  );
}
