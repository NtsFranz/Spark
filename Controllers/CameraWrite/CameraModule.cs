using System;
using System.Threading.Tasks;

namespace Spark
{
	public abstract class CameraModule
	{
		public bool Enabled
		{
			get => _enabled;
			set
			{
				switch (value)
				{
					case true when !_enabled:
						// Program.cameraController.OnUpdate += Update;
						Program.cameraController.updateCallbacks.Add(Update);
						OnEnabled?.Invoke();
						break;
					case false when _enabled:
						// Program.cameraController.OnUpdate -= Update;
						Program.cameraController.updateCallbacks.Remove(Update);
						OnDisabled?.Invoke();
						break;
				}

				_enabled = value;
			}
		}

		private bool _enabled;
		protected Action OnEnabled;
		protected Action OnDisabled;

		protected abstract Task Update(CameraTransform cameraTransform, float deltaTime);
	}
}