using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using static Spark.CameraWrite;

namespace Spark
{
    public class BezierSpline
    {
        public CameraTransform[] keyframes;
        public List<BezierCurve> curves;

        public BezierSpline(IReadOnlyList<CameraTransform> keyframes)
        {
            List<CameraTransform> newKeyframes = new();
            List<CameraTransform> currentCurve = new();
            curves = new List<BezierCurve>();
            
            // add extra handles
            if (keyframes.Count > 2)
            {
                // split each node in a set of triplets
                // newKeyframes.Add(keyframes[0]);
                currentCurve.Add(keyframes[0]);
                for (int i = 1; i < keyframes.Count - 1; i++)
                {
                    Vector3 dir = keyframes[i + 1].position - keyframes[i - 1].position;
                    CameraTransform handle1 = new(keyframes[i].position - dir / 4,
                        Quaternion.Lerp(keyframes[i - 1].rotation, keyframes[i].rotation, .5f));

                    CameraTransform handle2 = new(keyframes[i].position + dir / 4,
                        Quaternion.Lerp(keyframes[i].rotation, keyframes[i + 1].rotation, .5f));

                    currentCurve.Add(handle1);
                    currentCurve.Add(keyframes[i]);
                    curves.Add(new BezierCurve(currentCurve));
                    currentCurve.Clear();
                    currentCurve.Add(keyframes[i]);
                    currentCurve.Add(handle2);
                    
                    // newKeyframes.Add(handle1);
                    // newKeyframes.Add(keyframes[i]);
                    // newKeyframes.Add(handle2);
                }
                
                currentCurve.Add(keyframes.Last());
                curves.Add(new BezierCurve(currentCurve));
                // newKeyframes.Add(keyframes.Last());
            }
            else
            {
                curves.Add(new BezierCurve(keyframes[0], keyframes[1]));
            }
            
            // // chunk these new keyframes into curves
            // while (keyframes.Count > 0)
            // {
            //     switch (keyframes.Count)
            //     {
            //         case 1:
            //             new MessageBox("Only one keyframe left").Show();
            //             break;
            //         case 2:
            //             curves.Add(new BezierCurve(new CameraTransform[]
            //             {
            //                 keyframes[0], keyframes[1]
            //             }));
            //             break;
            //         case 3:
            //             curves.Add(new BezierCurve(new CameraTransform[]
            //             {
            //                 keyframes[0], keyframes[1], keyframes[2]
            //             }));
            //             break;
            //         case 4:
            //             curves.Add(new BezierCurve(new CameraTransform[]
            //             {
            //                 keyframes[0], keyframes[1], keyframes[2], keyframes[3]
            //             }));
            //             break;
            //     }
            // }

            // this.keyframes = newKeyframes.ToArray();
        }

        public int CurveCount => (keyframes.Length - 1) / 3;

        public Vector3 GetPoint(float t)
        {
            // find out which curve we are using
            t = Clamp01(t) * curves.Count;
            int i = (int) t;
            t -= i;
            
            // avoid an error for t=1
            if (i == curves.Count) i = curves.Count - 1;
            
            // sample from that curve
            return curves[i].GetPoint(t);


            // switch (keyframes.Length)
            // {
            //     case 1:
            //         return keyframes[0].position;
            //     case 2:
            //         return Bezier.GetPoint(keyframes[0].position, keyframes[1].position, t);
            //     case 3:
            //         return Bezier.GetPoint(keyframes[0].position, keyframes[1].position, keyframes[2].position, t);
            //     default:
            //     {
            //         t = Clamp01(t) * CurveCount;
            //         int i = (int) t;
            //         t -= i;
            //         i *= 3;
            //         return Bezier.GetPoint(keyframes[i].position, keyframes[i + 1].position, keyframes[i + 2].position,
            //             t);
            //     }
            // }
        }

        public Quaternion GetRotation(float t)
        {
            
            // find out which curve we are using
            t = Clamp01(t) * curves.Count;
            int i = (int) t;
            t -= i;
            
            // avoid an error for t=1
            if (i == curves.Count) i = curves.Count - 1;
            
            // sample from that curve
            return curves[i].GetRotation(t);
            
            
            // // if this is the endpoint
            // if (t >= 1f)
            // {
            //     return keyframes.Last().rotation;
            // }
            //
            // switch (keyframes.Length)
            // {
            //     case 1:
            //         return keyframes[0].rotation;
            //     case 2:
            //         return Bezier.GetRotation(keyframes[0].rotation, keyframes[1].rotation, t);
            //     case 3:
            //         return Bezier.GetRotation(keyframes[0].rotation, keyframes[1].rotation, keyframes[2].rotation, t);
            //     default:
            //     {
            //         // 
            //         t = Clamp01(t) * CurveCount;
            //         int i = (int) t;
            //         t -= i;
            //         i *= 3;
            //         return Bezier.GetRotation(keyframes[i].rotation, keyframes[i + 1].rotation,
            //             keyframes[i + 2].rotation, t);
            //     }
            // }
        }

        public Vector3 GetVelocity(float t)
        {
            int i;
            if (t >= 1f)
            {
                t = 1f;
                i = keyframes.Length - 4;
            }
            else
            {
                t = Clamp01(t) * CurveCount;
                i = (int) t;
                t -= i;
                i *= 3;
            }

            return Bezier.GetFirstDerivative(keyframes[i].position, keyframes[i + 1].position,
                keyframes[i + 2].position, keyframes[i + 3].position, t);
        }

        public Vector3 GetDirection(float t)
        {
            return GetVelocity(t).Normalized();
        }


        private static float Clamp01(float t)
        {
            if (t > 1) return 1;
            else if (t < 0) return 0;
            else return t;
        }
    }

    public class BezierCurve
    {
        public CameraTransform[] keyframes;

        
        public BezierCurve(IEnumerable<CameraTransform> keyframes)
        {
            this.keyframes = keyframes.ToArray();
        }
        
        public BezierCurve(params CameraTransform[] keyframes)
        {
            this.keyframes = keyframes;
        }

        public float ArcLength()
        {
            return 0;
        }

        public Vector3 GetPoint(float t)
        {
            return keyframes.Length switch
            {
                1 => keyframes[0].position,
                2 => Bezier.GetPoint(keyframes[0].position, keyframes[1].position, t),
                3 => Bezier.GetPoint(keyframes[0].position, keyframes[1].position, keyframes[2].position, t),
                4 => Bezier.GetPoint(keyframes[0].position, keyframes[1].position, keyframes[2].position,
                    keyframes[3].position, t),
                _ => Vector3.Zero
            };
        }

        public Quaternion GetRotation(float t)
        {
            return keyframes.Length switch
            {
                1 => keyframes[0].rotation,
                2 => Bezier.GetRotation(keyframes[0].rotation, keyframes[1].rotation, t),
                3 => Bezier.GetRotation(keyframes[0].rotation, keyframes[1].rotation, keyframes[2].rotation, t),
                4 => Bezier.GetRotation(keyframes[0].rotation, keyframes[1].rotation, keyframes[2].rotation,
                    keyframes[3].rotation, t),
                _ => Quaternion.Identity
            };
        }
    }

    public static class Bezier
    {
        private static float Clamp01(float t)
        {
            if (t > 1) return 1;
            else if (t < 0) return 0;
            else return t;
        }

        public static Vector3 GetPoint(Vector3 p0, Vector3 p1, float t)
        {
            t = Clamp01(t);
            return Vector3.Lerp(p0, p1, t);
        }

        public static Quaternion GetRotation(Quaternion p0, Quaternion p1, float t)
        {
            t = Clamp01(t);
            return Quaternion.Lerp(p0, p1, t);
        }

        public static Vector3 GetPoint(Vector3 p0, Vector3 p1, Vector3 p2, float t)
        {
            t = Clamp01(t);
            float oneMinusT = 1f - t;
            return
                oneMinusT * oneMinusT * p0 +
                2f * oneMinusT * t * p1 +
                t * t * p2;
        }

        public static Quaternion GetRotation(Quaternion p0, Quaternion p1, Quaternion p2, float t)
        {
            t = Clamp01(t);
            return Quaternion.Lerp(Quaternion.Lerp(p0, p1, t), Quaternion.Lerp(p1, p2, t), t);
        }


        public static Vector3 GetFirstDerivative(Vector3 p0, Vector3 p1, Vector3 p2, float t)
        {
            return
                2f * (1f - t) * (p1 - p0) +
                2f * t * (p2 - p1);
        }

        public static Vector3 GetPoint(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            t = Clamp01(t);
            float OneMinusT = 1f - t;
            return
                OneMinusT * OneMinusT * OneMinusT * p0 +
                3f * OneMinusT * OneMinusT * t * p1 +
                3f * OneMinusT * t * t * p2 +
                t * t * t * p3;
        }

        public static Quaternion GetRotation(Quaternion p0, Quaternion p1, Quaternion p2, Quaternion p3, float t)
        {
            t = Clamp01(t);
            return Quaternion.Lerp(Quaternion.Lerp(Quaternion.Lerp(p0, p1, t), Quaternion.Lerp(p1, p2, t), t),
                Quaternion.Lerp(Quaternion.Lerp(p1, p2, t), Quaternion.Lerp(p2, p3, t), t), t);
        }

        public static Vector3 GetFirstDerivative(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            t = Clamp01(t);
            float oneMinusT = 1f - t;
            return
                3f * oneMinusT * oneMinusT * (p1 - p0) +
                6f * oneMinusT * t * (p2 - p1) +
                3f * t * t * (p3 - p2);
        }
    }
}