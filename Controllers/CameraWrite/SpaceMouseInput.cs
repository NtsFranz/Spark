using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using EchoVRAPI;

namespace Spark
{
	public class SpaceMouseInput : CameraModule
	{
		public Action<HIDDeviceInput.ConnexionState> InputChanged;
		private CameraTransform spaceMouseCameraState = new CameraTransform();
		public HIDDeviceInput.ConnexionState lastMouseState = new HIDDeviceInput.ConnexionState();

		private readonly HIDDeviceInput spaceMouseDevice = new HIDDeviceInput(new List<HIDDeviceInput.Device>()
		{
			new HIDDeviceInput.Device { name = "SpaceNavigator", vendor = 0x46d, product = 0xc626 },
			new HIDDeviceInput.Device { name = "SpaceMouse Compact", vendor = 0x256F, product = 0xc635 },
			new HIDDeviceInput.Device { name = "CadMouse Wireless", vendor = 0x256F, product = 0xc651 },
			new HIDDeviceInput.Device { name = "3DConnexion Universal Receiver", vendor = 0x256F, product = 0xc652 },
			new HIDDeviceInput.Device { name = "3DConnexion SpaceMouse Wireless Receiver", vendor = 0x256F, product = 0xc62f },
		});

		public SpaceMouseInput()
		{
			spaceMouseDevice.OnChanged += bytes =>
			{
				HIDDeviceInput.ConnexionState state = new HIDDeviceInput.ConnexionState();
				switch (bytes[0])
				{
					case 1:
						state.position = new Vector3(
							(short)((bytes[2] << 8) | bytes[1]) / 350f,
							(short)((bytes[4] << 8) | bytes[3]) / 350f,
							(short)((bytes[6] << 8) | bytes[5]) / 350f
						);
						lastMouseState.position = state.position;
						break;
					case 2:
						state.rotation = new Vector3(
							(short)((bytes[2] << 8) | bytes[1]) / 350f,
							(short)((bytes[4] << 8) | bytes[3]) / 350f,
							(short)((bytes[6] << 8) | bytes[5]) / 350f
						);
						lastMouseState.rotation = state.rotation;
						break;
					case 3:
						state.leftClick = (bytes[1] & 1) != 0;
						state.rightClick = (bytes[1] & 2) != 0;
						lastMouseState.leftClick = state.leftClick;
						lastMouseState.rightClick = state.rightClick;
						break;
				}

				Vector3 inputPosition = new Vector3(
					CameraWriteController.Exponential(-state.position.X * CameraWriteSettings.instance.spaceMouseMoveSpeed, CameraWriteSettings.instance.spaceMouseMoveExponential),
					CameraWriteController.Exponential(-state.position.Z * CameraWriteSettings.instance.spaceMouseMoveSpeed, CameraWriteSettings.instance.spaceMouseMoveExponential),
					CameraWriteController.Exponential(-state.position.Y * CameraWriteSettings.instance.spaceMouseMoveSpeed, CameraWriteSettings.instance.spaceMouseMoveExponential)
				);
				Quaternion rotate = Quaternion.CreateFromYawPitchRoll(
					CameraWriteController.Exponential(-state.rotation.Z * CameraWriteSettings.instance.spaceMouseRotateSpeed, CameraWriteSettings.instance.spaceMouseRotateExponential),
					CameraWriteController.Exponential(-state.rotation.X * CameraWriteSettings.instance.spaceMouseRotateSpeed, CameraWriteSettings.instance.spaceMouseRotateExponential),
					CameraWriteController.Exponential(-state.rotation.Y * CameraWriteSettings.instance.spaceMouseRotateSpeed, CameraWriteSettings.instance.spaceMouseRotateExponential)
				);

				Matrix4x4 camPosMatrix = Matrix4x4.CreateFromQuaternion(spaceMouseCameraState.Rotation);

				spaceMouseCameraState.Position += Vector3.Transform(inputPosition, camPosMatrix);
				spaceMouseCameraState.Rotation = Quaternion.Multiply(spaceMouseCameraState.Rotation, rotate);

				InputChanged?.Invoke(state);
			};

			OnEnabled += () =>
			{
				FetchUtils.GetRequestCallback("http://127.0.0.1:6721/session", null, response =>
				{
					if (response == null) return;
					Frame frame = Frame.FromJSON(DateTime.UtcNow, response, null);
					if (frame == null) return;
					(Vector3 p, Quaternion q) = frame.GetCameraTransform();
					spaceMouseCameraState = new CameraTransform(p, q);
				});
				spaceMouseDevice.Start();
			};

			OnDisabled += () => { spaceMouseDevice.Stop(); };
		}

		protected override async Task Update(CameraTransform cameraTransform, float deltaTime)
		{
			cameraTransform.Position = spaceMouseCameraState.Position;
			cameraTransform.Rotation = spaceMouseCameraState.Rotation;
		}
	}
}