using Rhino.Geometry;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ACORNSpraying.Miscellaneous;

namespace ACORNSpraying
{
    [Serializable]
    public class SprayCurve
    {
        public Curve Curve { get; set; } = null;

        private double _speed = -1;

        public double Speed
        {
            get
            {
                return _speed;
            }
            set
            {
                _speed = value;

                if (SprayPath.GlobalMaxSpeed < 0 || SprayPath.GlobalMaxSpeed < value)
                {
                    SprayPath.GlobalMaxSpeed = value;
                }

                if (SprayPath.GlobalMinSpeed < 0 || SprayPath.GlobalMinSpeed > value)
                {
                    SprayPath.GlobalMinSpeed = value;
                }
            }
        }

        public bool IsConnector { get; set; } = false;

        public bool IsEdge { get; set; } = false;

        public double EdgeAngle { get; set; } = 10 / 180 * Math.PI;

        public SprayCurve(Curve curve)
        {
            Curve = curve;
        }

        public SprayCurve(Point3d start, Point3d end)
        {
            Curve = new LineCurve(start, end);
        }
    }

    [Serializable]
    public class SprayPath : IList<SprayCurve>
    {
        private List<SprayCurve> sprayCurves;

        public SprayPath()
        {
            sprayCurves = new List<SprayCurve>();
        }

        public SprayCurve this[int index] { get => sprayCurves[index]; set => sprayCurves[index] = value; }

        public static double GlobalMinSpeed { get; set; } = -1;
        public static double GlobalMaxSpeed { get; set; } = -1;

        public double MinSpeed { get => sprayCurves.Min(x => x.Speed); }

        public double MaxSpeed { get => sprayCurves.Max(x => x.Speed); }

        public int Count => sprayCurves.Count;

        public bool IsReadOnly => false;

        public void Add(SprayCurve item)
        {
            sprayCurves.Add(item);
        }

        public void Clear()
        {
            sprayCurves.Clear();
        }

        public bool Contains(SprayCurve item)
        {
            return sprayCurves.Contains(item);
        }

        public void CopyTo(SprayCurve[] array, int arrayIndex)
        {
            sprayCurves.CopyTo(array, arrayIndex);
        }

        public IEnumerator<SprayCurve> GetEnumerator()
        {
            foreach (SprayCurve curve in sprayCurves)
            {
                yield return curve;
            }
        }

        public int IndexOf(SprayCurve item)
        {
            return sprayCurves.IndexOf(item);
        }

        public void Insert(int index, SprayCurve item)
        {
            sprayCurves.Insert(index, item);
        }

        public bool Remove(SprayCurve item)
        {
            return sprayCurves.Remove(item);
        }

        public void RemoveAt(int index)
        {
            sprayCurves.RemoveAt(index);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Reverse()
        {
            foreach (var curve in sprayCurves)
                curve.Curve.Reverse();

            sprayCurves.Reverse();
        }

        public double GetLength()
        {
            return sprayCurves.Sum(x => x.Curve.GetLength());
        }

        public double GetDuration()
        {
            return sprayCurves.Sum(x => x.Curve.GetLength() / x.Speed);
        }

        public void Translate(double x, double y, double z)
        {
            foreach (var curve in sprayCurves)
                curve.Curve.Translate(x, y, z);
        }

        public void TrimConnectors()
        {
            sprayCurves = sprayCurves.Where(x => !x.IsConnector).ToList();
        }

        public void AvoidHoles(Brep surf, Surface extSurf, double dist)
        {
            var holes = OffsetSurfHoles(surf, extSurf, dist);

            foreach (var c in sprayCurves)
            {
                if (c.IsConnector)
                    c.Curve = c.Curve.AvoidHoles(holes);
            }
        }

        public void AvoidHoles(List<Curve> holes)
        {
            foreach (var c in sprayCurves)
            {
                if (c.IsConnector)
                    c.Curve = c.Curve.AvoidHoles(holes);
            }
        }

        public Point3d PointAtStart()
        {
            if (sprayCurves.Count < 1)
                return new Point3d();

            return sprayCurves.First().Curve.PointAtStart;
        }
        public Point3d PointAtEnd()
        {
            if (sprayCurves.Count < 1)
                return new Point3d();

            return sprayCurves.Last().Curve.PointAtEnd;
        }
    }
}
