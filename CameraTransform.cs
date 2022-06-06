using System;
using System.Collections.Generic;
using System.Numerics;
using Newtonsoft.Json;

namespace Spark
{
	[Serializable]
	[JsonConverter(typeof(CameraTransformConverter))]
	public class CameraTransform
	{
		public CameraTransform()
		{
		}

		public CameraTransform(Vector3 position, Quaternion rotation, float fov = 1)
		{
			px = position.X;
			py = position.Y;
			pz = position.Z;

			qx = rotation.X;
			qy = rotation.Y;
			qz = rotation.Z;
			qw = rotation.W;

			fovy = fov;
		}

		public float? px;
		public float? py;
		public float? pz;

		public float? qx;
		public float? qy;
		public float? qz;
		public float? qw;

		public float? fovy = 1;

		public Vector3 Position
		{
			get => new Vector3(px ?? 0, py ?? 0, pz ?? 0);
			set
			{
				px = value.X;
				py = value.Y;
				pz = value.Z;
			}
		}

		public Quaternion Rotation
		{
			get => new Quaternion(qx ?? 0, qy ?? 0, qz ?? 0, qw ?? 1);
			set
			{
				qx = value.X;
				qy = value.Y;
				qz = value.Z;
				qw = value.W;
			}
		}
	}

	public class CameraTransformConverter : JsonConverter
	{
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			if (!(value is CameraTransform t)) return;
			writer.WriteStartObject();
			string[] vars = {
				"px",
				"py",
				"pz",
				"qx",
				"qy",
				"qz",
				"qw",
				"fovy"
			};
			foreach (string v in vars)
			{
				// reflection 🤢
				object result = t.GetType().GetField(v)?.GetValue(t);
				if (result == null) continue;
				writer.WritePropertyName(v);
				serializer.Serialize(writer, result);
			}

			writer.WriteEndObject();
			
			// CameraTransform t = (CameraTransform)value;
			//
			// Dictionary<string, float> output = new Dictionary<string, float>();
			//
			// if (t.px != null) output["px"] = t.px ?? 0;
			// if (t.py != null) output["py"] = t.py ?? 0;
			// if (t.pz != null) output["pz"] = t.pz ?? 0;
			//
			// if (t.qx != null) output["qx"] = t.qx ?? 0;
			// if (t.qy != null) output["qy"] = t.qy ?? 0;
			// if (t.qz != null) output["qz"] = t.qz ?? 0;
			// if (t.qw != null) output["qx"] = t.qw ?? 1;
			//
			// if (t.fovy != null) output["fovy"] = t.fovy ?? 1;
			//
			// writer.WriteValue(JsonConvert.SerializeObject(output));
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
		}

		public override bool CanRead => false;

		public override bool CanConvert(Type objectType)
		{
			return typeof(CameraTransform).IsAssignableFrom(objectType);
		}
	}


	[Serializable]
	public class AnimationKeyframes
	{
		public string description;
		public float duration;
		public bool easeIn;
		public bool easeOut;
		public bool pauseWhenClockNotRunning;
		public List<CameraTransform> keyframes;

		public AnimationKeyframes()
		{
			description = "";
			duration = 5;
			keyframes = new List<CameraTransform>();
		}

		public AnimationKeyframes Copy()
		{
			return new AnimationKeyframes()
			{
				description = description,
				duration = duration,
				easeIn = easeIn,
				easeOut = easeOut,
				keyframes = new List<CameraTransform>(keyframes)
			};
		}
	}
}