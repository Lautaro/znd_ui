using System.Collections.Generic;
using UnityEngine;

namespace Znd.Mock {

    /// <summary>One point of a mock curve. A "random" point also has how far a play may move it (drawn as an ellipse).</summary>
    public class CurvePoint {
        public float time, value;
        public float bend = 1f;            // shape of the segment arriving at this point (1 = straight)
        public float randomX, randomY;     // 0 = not random
        public float bias = 0.5f;
        public CurvePoint(float time, float value) { this.time = time; this.value = value; }
    }

    /// <summary>
    /// A plain list of points in a box (x from xMin to xMax, y from yMin to yMax), joined by straight or bent segments.
    /// Mock data only: it is what the curve editors draw and drag, nothing reads it to make sound.
    /// </summary>
    public class Curve {
        public readonly List<CurvePoint> points = new List<CurvePoint>();
        public float xMin, xMax = 1f, yMin, yMax = 1f;

        public Curve(float xMin, float xMax, float yMin, float yMax, params Vector2[] pts) {
            this.xMin = xMin; this.xMax = xMax; this.yMin = yMin; this.yMax = yMax;
            foreach (var p in pts) points.Add(new CurvePoint(p.x, p.y));
        }

        public int Count => points.Count;

        public float Evaluate(float t) {
            if (points.Count == 0) return (yMin + yMax) * 0.5f;
            if (t <= points[0].time) return points[0].value;
            for (int i = 1; i < points.Count; i++) {
                var b = points[i];
                if (t <= b.time) {
                    var a = points[i - 1];
                    float span = Mathf.Max(1e-6f, b.time - a.time);
                    float u = Mathf.Pow(Mathf.Clamp01((t - a.time) / span), Mathf.Max(0.05f, b.bend));
                    return Mathf.Lerp(a.value, b.value, u);
                }
            }
            return points[points.Count - 1].value;
        }

        public CurvePoint Add(float time, float value) {
            var p = new CurvePoint(Mathf.Clamp(time, xMin, xMax), Mathf.Clamp(value, yMin, yMax));
            int i = 0;
            while (i < points.Count && points[i].time <= p.time) i++;
            points.Insert(i, p);
            return p;
        }

        public void Remove(CurvePoint p) {
            // The first and last points hold the curve's ends and are never removed.
            int i = points.IndexOf(p);
            if (i <= 0 || i >= points.Count - 1) return;
            points.RemoveAt(i);
        }

        /// <summary>The index of the first point at or after <paramref name="t"/>.</summary>
        public int SegmentAt(float t) {
            for (int i = 0; i < points.Count; i++) if (points[i].time >= t) return i;
            return points.Count - 1;
        }
    }
}
