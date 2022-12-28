using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using EchoVRAPI;

namespace Spark
{
	public class DiscOrbit : CameraModule
	{
		private readonly DateTime startTime = DateTime.UtcNow;
		private Vector3 lastDiscPos = Vector3.Zero;
		private Vector3 lastDiscVel = Vector3.Zero;
		private Vector3 smoothDiscPos = Vector3.Zero;
		const int avgCount = 5;
		private readonly List<CameraTransform> lastTransforms = new List<CameraTransform>();

		protected override async Task Update(CameraTransform cameraTransform, float deltaTime)
		{
			// do another API request for lowest latency
			DateTime currentTime = DateTime.UtcNow;
			string resp = await FetchUtils.GetRequestAsync("http://127.0.0.1:6721/session");
			Frame frame = Frame.FromJSON(currentTime, resp, null);
			if (frame == null) return;

			double elapsed = (currentTime - startTime).TotalSeconds;

			double angle = elapsed * CameraWriteSettings.instance.rotSpeed * CameraWriteController.Deg2Rad;


			Vector3 discPos = frame.disc.position.ToVector3();
			Vector3 discVel = frame.disc.velocity.ToVector3();

			Vector3 diff = discPos - lastDiscPos;
			if (discVel != Vector3.Zero)
			{
				diff = discVel;
			}

			Quaternion vel;
			Vector3 offset;
			if (diff == Vector3.Zero)
			{
				vel = Quaternion.Identity;
				offset = Vector3.UnitZ;
			}
			else
			{
				vel = CameraWriteController.QuaternionLookRotation(diff.Normalized(), Vector3.UnitY);
				offset = diff.Normalized();
			}

			offset = new Vector3((float)Math.Cos(angle), 0, (float)Math.Sin(angle)) * CameraWriteSettings.instance.orbitRadius;

			// add lag comp
			discPos += discVel * CameraWriteSettings.instance.lagCompDiscFollow;
			lastDiscPos += lastDiscVel * CameraWriteSettings.instance.lagCompDiscFollow;

			//discPos = Vector3.Lerp(lastDiscPos, discPos, (float)(DateTime.Now - Program.lastDataTime).TotalSeconds/1);
			smoothDiscPos = Vector3.Lerp(smoothDiscPos, discPos, CameraWriteSettings.instance.followSmoothing);
			discPos = smoothDiscPos;

			Vector3 pos = discPos - offset;

			Quaternion lookDir = CameraWriteController.QuaternionLookRotation(discPos - pos, Vector3.UnitY);

			CameraTransform newTransform = new CameraTransform(pos, lookDir);
			lastTransforms.Add(newTransform);
			if (lastTransforms.Count > avgCount)
			{
				lastTransforms.RemoveAt(0);
			}

			CameraTransform avg = lastTransforms[0];

			foreach (CameraTransform t in lastTransforms)
			{
				//avg.position = Vector3.Lerp(lastTransforms[i].position, avg.position, .5f);
				avg.Position += t.Position;
				avg.Rotation = Quaternion.Lerp(t.Rotation, avg.Rotation, .5f);
			}

			avg.Position /= avgCount;

			avg.Rotation = new Quaternion(avg.Rotation.X / avgCount, avg.Rotation.Y / avgCount,
				avg.Rotation.Z / avgCount, avg.Rotation.W / avgCount);
			//avg.rotation /= (float)avgCount;


			cameraTransform.Position = newTransform.Position;
			cameraTransform.Rotation = newTransform.Rotation;

			lastDiscPos = discPos;
			lastDiscVel = discVel;
		}
	}
}