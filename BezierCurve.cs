using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;

namespace Spark
{
	public class BezierSpline
	{
		public CameraTransform[] keyframes;
		public List<BezierCurve> curves;

		public BezierSpline(AnimationKeyframes animation)
		{
			List<CameraTransform> animKeyFrames = animation.keyframes;
			List<CameraTransform> currentCurve = new List<CameraTransform>();
			curves = new List<BezierCurve>();

			// add extra handles
			if (animKeyFrames.Count > 2)
			{
				// split each node in a set of triplets
				currentCurve.Add(animKeyFrames[0]);

				// loop through all keyframes except first and last
				for (int i = 1; i < animKeyFrames.Count - 1; i++)
				{
					Vector3 dir = animKeyFrames[i + 1].Position - animKeyFrames[i - 1].Position;
					Quaternion rotDiff = animKeyFrames[i + 1].Rotation - animKeyFrames[i - 1].Rotation;
					float fovDiff = animKeyFrames[i + 1].fovy ?? 1 - animKeyFrames[i - 1].fovy ?? 1;

					float divisionFactor = 6;
					float dist1 = Vector3.Distance(animKeyFrames[i + 1].Position, animKeyFrames[i].Position);
					float dist2 = Vector3.Distance(animKeyFrames[i - 1].Position, animKeyFrames[i].Position);
					if (Math.Min(dist1, dist2) / Math.Max(dist1, dist2) < .5f)
					{
						divisionFactor = 8;
					}

					CameraTransform handle1 = new CameraTransform(
						animKeyFrames[i].Position - dir / divisionFactor,
						// Quaternion.Lerp(keyframes[i - 1].Rotation, keyframes[i].Rotation, .5f)
						animKeyFrames[i].Rotation - rotDiff * .25f,
						animKeyFrames[i].fovy??1 - fovDiff / divisionFactor
					);
					CameraTransform handle2 = new CameraTransform(
						animKeyFrames[i].Position + dir / divisionFactor,
						// Quaternion.Lerp(keyframes[i].Rotation, keyframes[i + 1].Rotation, .5f)
						animKeyFrames[i].Rotation + rotDiff * .25f,
						animKeyFrames[i].fovy??1 + fovDiff / divisionFactor
					);

					currentCurve.Add(handle1);
					currentCurve.Add(animKeyFrames[i]);
					curves.Add(new BezierCurve(currentCurve));
					currentCurve.Clear();
					currentCurve.Add(animKeyFrames[i]);
					currentCurve.Add(handle2);
				}

				// add the last keyframe
				currentCurve.Add(animKeyFrames[^1]);
				curves.Add(new BezierCurve(currentCurve));
			}
			else if (animKeyFrames.Count > 1)
			{
				curves.Add(new BezierCurve(animKeyFrames[0], animKeyFrames[1]));
			}
			else if (animKeyFrames.Count > 0)
			{
				curves.Add(new BezierCurve(animKeyFrames[0]));
				Debug.WriteLine(
					"Single frame curve. This should only happen if there is one keyframe in the entire spline.");
			}
		}

		public (BezierCurve, float) GetCurve(float t)
		{
			t = Clamp01(t);
			BezierCurve targetCurve = null;
			float totalCurveLength = curves.Sum(c => c.ArcLength());
			float currentSum = 0;
			foreach (BezierCurve curve in curves)
			{
				float sumAfter = currentSum + curve.ArcLength();
				if (t <= sumAfter / totalCurveLength)
				{
					targetCurve = curve;
					t *= totalCurveLength;
					t -= currentSum;
					t /= sumAfter - currentSum;
					break;
				}

				currentSum = sumAfter;
			}

			return (targetCurve, t);
		}

		public Vector3 GetPoint(float t)
		{
			// find out which curve we are using
			BezierCurve curve;
			(curve, t) = GetCurve(t);

			// sample from that curve
			if (curve != null) return curve.GetPoint(t, true);

			Logger.LogRow(Logger.LogType.Error, "Error getting point from bezier");
			return Vector3.Zero;
		}

		public Quaternion GetRotation(float t)
		{
			// find out which curve we are using
			BezierCurve curve;
			(curve, t) = GetCurve(t);

			// sample from that curve
			if (curve != null) return curve.GetRotation(t, true);

			Logger.LogRow(Logger.LogType.Error, "Error getting point from bezier");
			return Quaternion.Identity;
		}

		public float GetFov(float t)
		{
			// find out which curve we are using
			BezierCurve curve;
			(curve, t) = GetCurve(t);

			// sample from that curve
			if (curve != null) return curve.GetFov(t, true);

			Logger.LogRow(Logger.LogType.Error, "Error getting point from bezier");
			return 1;
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
				t = Clamp01(t) * curves.Count;
				i = (int)t;
				t -= i;
				i *= 3;
			}

			return Bezier.GetFirstDerivative(keyframes[i].Position, keyframes[i + 1].Position,
				keyframes[i + 2].Position, keyframes[i + 3].Position, t);
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
		private float arcLength;

		/// <summary>
		/// The distance-adjusted t values at fixed original t spacing
		/// </summary>
		// ReSharper disable once InconsistentNaming
		private float[] LUT;


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
			if (LUT != null) return arcLength;
			CalculateLUT();
			return arcLength;
		}

		// ReSharper disable once InconsistentNaming
		private void CalculateLUT()
		{
			const int samples = 64;
			if (keyframes.Length == 1)
			{
				arcLength = 0;
				LUT = new float[] { 0 };
				return;
			}

			LUT = new float[samples];
			Vector3 lastPosition = keyframes[0].Position;
			LUT[0] = 0;
			for (int i = 1; i < samples; i++)
			{
				Vector3 newPoint = GetPoint((float)i / samples);
				arcLength += Vector3.Distance(lastPosition, newPoint);
				LUT[i] = arcLength;
				lastPosition = newPoint;
			}

			for (int i = 0; i < LUT.Length; i++)
			{
				LUT[i] /= arcLength;
			}

			Debug.WriteLine($"Arc Length for curve {keyframes[0].Position.X}: {arcLength}");
		}

		private float GetDistanceNormalizedT(float t)
		{
			if (Math.Abs(t - 1) < float.Epsilon) return 1;

			for (int i = 0; i < LUT.Length - 1; i++)
			{
				if (t >= LUT[i] && t < LUT[i + 1])
				{
					return t.Remap(
						LUT[i],
						LUT[i + 1],
						(float)i / (LUT.Length - 1),
						(float)(i + 1) / (LUT.Length - 1)
					);
				}
			}

			Debug.WriteLine("Something went wrong with LUT usage.");
			return LUT[(int)(t * LUT.Length)];
		}

		public Vector3 GetPoint(float t, bool distanceNormalized = false)
		{
			if (distanceNormalized) t = GetDistanceNormalizedT(t);

			return keyframes.Length switch
			{
				1 => keyframes[0].Position,
				2 => Bezier.GetPoint(keyframes[0].Position, keyframes[1].Position, t),
				3 => Bezier.GetPoint(keyframes[0].Position, keyframes[1].Position, keyframes[2].Position, t),
				4 => Bezier.GetPoint(keyframes[0].Position, keyframes[1].Position, keyframes[2].Position,
					keyframes[3].Position, t),
				_ => Vector3.Zero
			};
		}

		public Quaternion GetRotation(float t, bool distanceNormalized = false)
		{
			if (distanceNormalized) t = GetDistanceNormalizedT(t);

			return keyframes.Length switch
			{
				1 => keyframes[0].Rotation,
				2 => Bezier.GetRotation(keyframes[0].Rotation, keyframes[1].Rotation, t),
				3 => Bezier.GetRotation(keyframes[0].Rotation, keyframes[1].Rotation, keyframes[2].Rotation, t),
				4 => Bezier.GetRotation(keyframes[0].Rotation, keyframes[1].Rotation, keyframes[2].Rotation,
					keyframes[3].Rotation, t),
				_ => Quaternion.Identity
			};
		}

		public float GetFov(float t, bool distanceNormalized = false)
		{
			if (distanceNormalized) t = GetDistanceNormalizedT(t);

			return keyframes.Length switch
			{
				1 => keyframes[0].fovy??1,
				2 => Bezier.GetFov(keyframes[0].fovy??1, keyframes[1].fovy??1, t),
				3 => Bezier.GetFov(keyframes[0].fovy??1, keyframes[1].fovy??1, keyframes[2].fovy??1, t),
				4 => Bezier.GetFov(keyframes[0].fovy??1, keyframes[1].fovy??1, keyframes[2].fovy??1,
					keyframes[3].fovy??1, t),
				_ => 1
			};
		}
	}

	public static class Bezier
	{
		private static float Clamp01(float t)
		{
			return t switch
			{
				> 1 => 1,
				< 0 => 0,
				_ => t
			};
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

		public static float GetFov(float p0, float p1, float t)
		{
			t = Clamp01(t);
			return Lerp(p0, p1, t);
		}

		private static float Lerp(float p0, float p1, float t)
		{
			return p0 + t * (p1 - p0);
		}

		/// <summary>
		/// 3 positions
		/// </summary>
		public static Vector3 GetPoint(Vector3 p0, Vector3 p1, Vector3 p2, float t)
		{
			// t = Clamp01(t);
			// float oneMinusT = 1f - t;
			// return
			//     oneMinusT * oneMinusT * p0 +
			//     2f * oneMinusT * t * p1 +
			//     t * t * p2;


			t = Clamp01(t);
			return Vector3.Lerp(Vector3.Lerp(p0, p1, t), Vector3.Lerp(p1, p2, t), t);
		}

		/// <summary>
		/// 3 rotations
		/// </summary>
		public static Quaternion GetRotation(Quaternion p0, Quaternion p1, Quaternion p2, float t,
			bool distanceNormalized = false)
		{
			t = Clamp01(t);
			return Quaternion.Lerp(Quaternion.Lerp(p0, p1, t), Quaternion.Lerp(p1, p2, t), t);
		}

		public static float GetFov(float p0, float p1, float p2, float t,
			bool distanceNormalized = false)
		{
			t = Clamp01(t);
			return Lerp(Lerp(p0, p1, t), Lerp(p1, p2, t), t);
		}


		public static Vector3 GetFirstDerivative(Vector3 p0, Vector3 p1, Vector3 p2, float t)
		{
			return
				2f * (1f - t) * (p1 - p0) +
				2f * t * (p2 - p1);
		}


		/// <summary>
		/// 4 positions
		/// </summary>
		public static Vector3 GetPoint(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
		{
			// t = Clamp01(t);
			// float OneMinusT = 1f - t;
			// return
			//     OneMinusT * OneMinusT * OneMinusT * p0 +
			//     3f * OneMinusT * OneMinusT * t * p1 +
			//     3f * OneMinusT * t * t * p2 +
			//     t * t * t * p3;


			t = Clamp01(t);
			return Vector3.Lerp(Vector3.Lerp(Vector3.Lerp(p0, p1, t), Vector3.Lerp(p1, p2, t), t),
				Vector3.Lerp(Vector3.Lerp(p1, p2, t), Vector3.Lerp(p2, p3, t), t), t);
		}


		/// <summary>
		/// 4 rotations
		/// </summary>
		public static Quaternion GetRotation(Quaternion p0, Quaternion p1, Quaternion p2, Quaternion p3, float t)
		{
			t = Clamp01(t);
			return Quaternion.Lerp(Quaternion.Lerp(Quaternion.Lerp(p0, p1, t), Quaternion.Lerp(p1, p2, t), t),
				Quaternion.Lerp(Quaternion.Lerp(p1, p2, t), Quaternion.Lerp(p2, p3, t), t), t);
		}

		public static float GetFov(float p0, float p1, float p2, float p3, float t)
		{
			t = Clamp01(t);
			return Lerp(Lerp(Lerp(p0, p1, t), Lerp(p1, p2, t), t),
				Lerp(Lerp(p1, p2, t), Lerp(p2, p3, t), t), t);
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

	public static class MathExtensions
	{
		public static float Remap(this float input, float inputMin, float inputMax, float outputMin, float outputMax)
		{
			if (input < inputMin || input > inputMax)
			{
				Debug.WriteLine("Input outside input range");
			}

			if (inputMax < inputMin)
			{
				Debug.WriteLine("Input max < min");
			}

			if (outputMax < outputMin)
			{
				Debug.WriteLine("Output max < min");
			}

			input -= inputMin;
			input /= (inputMax - inputMin);
			input *= (outputMax - outputMin);
			input += outputMin;
			return input;
		}
	}
}