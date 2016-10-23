using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

namespace vpet
{
	public partial class UI : MonoBehaviour
	{
		private RangeSlider slider;
		private CameraObject.CameraParameter sliderType;

        //! setup function for all the slider UIs in scout view
        private void setupCameraSlider()
        {
            // check if slider prefab is available
            GameObject rangeSliderPrefab = Resources.Load<GameObject>("VPET/Prefabs/RangeTemplate");
            if (rangeSliderPrefab == null) Debug.LogError(string.Format("{0}: Cannot load: RangeTemplate.", this.GetType()));
            else
            {
				// create camera slider object
                GameObject cameraSlider = Instantiate(rangeSliderPrefab);
				cameraSlider.name = rangeSliderPrefab.name;
				cameraSlider.transform.SetParent( secondaryMenu.transform, false ) ;
				cameraSlider.transform.localPosition = new Vector3(0, (-VPETSettings.Instance.canvasHalfHeight+2*UI.ButtonOffset)* VPETSettings.Instance.canvasAspectScaleFactor, 0);
				cameraSlider.transform.localScale = Vector3.one;
				cameraSlider.SetActive(false);

				//! slider instance for camera paramenters
				slider = cameraSlider.GetComponent<RangeSlider>();
            }
        }

		//! configure slider based on the chosen parameter
		private void setSliderType(CameraObject.CameraParameter type) 
		{
			//! set slider type
			sliderType = type;

			//! change slider setup
			switch (sliderType) {
			case CameraObject.CameraParameter.FOV:
				slider.Callback = sliderCallback;
				slider.MinValue = 300.0f.lensToVFov();
				slider.MaxValue = 10.0f.lensToVFov();
				slider.FormatAsInt = false;
				slider.Sensitivity = 0.3f;
				slider.TextPrefix = "Field of view: ";
				slider.TextSuffix = " degrees";
				break;
			case CameraObject.CameraParameter.LENS:
				slider.Callback = sliderCallback;
				slider.MinValue = 10.0f;
				slider.MaxValue = 300.0f;
				slider.FormatAsInt = true;
				slider.Sensitivity = 0.3f;
				slider.TextPrefix = "Lens focal length: ";
				slider.TextSuffix = "mm";
				break;
			case CameraObject.CameraParameter.APERTURE:
				slider.Callback = sliderCallback;
				slider.MaxValue = 1.0f;
				slider.MinValue = 0.0f;
				slider.FormatAsInt = false;
				slider.Sensitivity = 0.003f;
				slider.TextPrefix = "Aperture: ";
				slider.TextSuffix = "";
				break;
			case CameraObject.CameraParameter.FOCSIZE:
				slider.Callback = sliderCallback;
				slider.MaxValue = 1.0f;
				slider.MinValue = 0.0f;
				slider.FormatAsInt = false;
				slider.Sensitivity = 0.003f;
				slider.TextPrefix = "Focal size: ";
				slider.TextSuffix = "m";
				break;
			case CameraObject.CameraParameter.FOCDIST:
				slider.Callback = sliderCallback;
				slider.MaxValue = 100.0f;
				slider.MinValue = 0.0f;
				slider.FormatAsInt = false;
				slider.Sensitivity = 0.05f;
				slider.TextPrefix = "Focal distance: ";
				slider.TextSuffix = "m";
				break;
			default:
				break;
			}
		}

		//! callback on slider update
		public void sliderCallback(float value)
		{
			mainController.setCamParamValue (sliderType, value);
		}

		//! show desired camera parameter slider
		public void showCameraSlider(CameraObject.CameraParameter type)
		{
			// if the desired slider is not already displayed, show it
			if (slider.IsActive == false || type != sliderType) {
				// disable callback temporarily, sync slider value with the camera parameter
				slider.Callback = null;
				setSliderType (type);
				
				float newValue = mainController.getCamParamValue (type);
				updateSliderValue (newValue);
				
				// show slider
				slider.gameObject.SetActive (true);
			} else {
				// ... hide the slider, since the user pressed the button again
				hideCameraSlider();
			}
		}

		//! hide the camera parameter slider
		public void hideCameraSlider()
		{
			slider.gameObject.SetActive(false);
		}

		//! update slider value with supplied value
		public void updateSliderValue(float value)
		{
			slider.Value = value;
		}

		//! preparing / showing the desired slider on button press

		public void toggleLens()
		{
			showCameraSlider(CameraObject.CameraParameter.LENS);
		}

		public void toggleFocus()
		{
			showCameraSlider(CameraObject.CameraParameter.FOCDIST);
		}

		public void toggleAperture()
		{
			showCameraSlider(CameraObject.CameraParameter.APERTURE);
		}
	}
}
