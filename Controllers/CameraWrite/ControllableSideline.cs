using System;
using System.Numerics;
using System.Threading.Tasks;

namespace Spark
{
	public class ControllableSideline : CameraModule
	{
		private Vector3 smoothedDiscPos = Vector3.Zero;
		private Vector3 smoothedDiscPosDirection = Vector3.Zero;
		private float discPositionSmoothness = 2f;
		private float discPositionSmoothnessDirection = 3f;

		protected override async Task Update(CameraTransform cameraTransform, float deltaTime)
		{
			if (Program.lastFrame == null) return;

			smoothedDiscPos = Vector3.Lerp(smoothedDiscPos, Program.lastFrame.disc.Position, deltaTime * discPositionSmoothness);
			smoothedDiscPosDirection = Vector3.Lerp(smoothedDiscPosDirection, Program.lastFrame.disc.Position, deltaTime * discPositionSmoothnessDirection);

			Vector3 pos = smoothedDiscPos;

			pos.Z = Math.Clamp(pos.Z, -24, 24);
			pos.X = 14.4f;
			pos.Y = pos.Z * pos.Z * .009f;
			Vector3 direction = smoothedDiscPosDirection - pos;
			Quaternion rotation = CameraWriteController.QuaternionLookRotation(direction, Vector3.UnitY);
			cameraTransform.fovy = Math.Clamp(20f / Vector3.Distance(pos, smoothedDiscPos), .2f, 1.2f);
			cameraTransform.Position = pos;
			cameraTransform.Rotation = rotation;
		}
	}
}