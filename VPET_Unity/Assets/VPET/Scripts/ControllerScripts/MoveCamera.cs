/*
-----------------------------------------------------------------------------
This source file is part of VPET - Virtual Production Editing Tool
http://vpet.research.animationsinstitut.de/
http://github.com/FilmakademieRnd/VPET

Copyright (c) 2016 Filmakademie Baden-Wuerttemberg, Institute of Animation

This project has been realized in the scope of the EU funded project Dreamspace
under grant agreement no 610005.
http://dreamspaceproject.eu/

This program is free software; you can redistribute it and/or modify it under
the terms of the GNU Lesser General Public License as published by the Free Software
Foundation; version 2.1 of the License.

This program is distributed in the hope that it will be useful, but WITHOUT
ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS
FOR A PARTICULAR PURPOSE. See the GNU Lesser General Public License for more details.

You should have received a copy of the GNU Lesser General Public License along with
this program; if not, write to the Free Software Foundation, Inc., 59 Temple
Place - Suite 330, Boston, MA 02111-1307, USA, or go to
http://www.gnu.org/licenses/old-licenses/lgpl-2.1.html
-----------------------------------------------------------------------------
*/
using UnityEngine;
using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityStandardAssets.ImageEffects;

//!
//! This class provides all functionallities for the principal camera including the real world rotation functionality
//!
namespace vpet
{
    public class MoveCamera : MonoBehaviour
    {

#if UNITY_STANDALONE_WIN
	    //!
	    //! Setup the connection to the sensors and initalize sensor fusion.
	    //! C style binding to external c++ library to read sensor values on Windows (without deploying it for windows store).
	    //!
	    [DllImport("WinSensorPlugin", EntryPoint = "initalizeSensorReading")]
	    private static extern void initalizeSensorReading();
	
	    //!
	    //! Read sensor fusion value for device orientation based on gyroscope, compass and accelerometer
	    //! C style binding to external c++ library to read sensor values on windows (without deploying it for windows store)
	    //!
	    [DllImport("WinSensorPlugin", EntryPoint="getOrientationSensorData")]
	    private static extern IntPtr getOrientationSensorData();
#endif

        //data reveived from different sensors on Windows or Android
#if UNITY_STANDALONE_WIN
	    //!
	    //! float vector holding the quaternion of the current device orientation when on a windows machine
	    //!
	    float[] orientationSensorData = new float[4];
#endif

        //!
        //! This variable can enable and disable the automatic sensor based camera rotation.
        //!
        private bool move = true;


        //!
        //! Reference to main controller
        //!
        private MainController mainController = null;


        //!
        //! Reference to server adapter to send out changes on execution
        //!
        private ServerAdapter serverAdapter;

        //!
        //! If a gameObject is attached to the camera, this value will hold the rotation of the object.
        //! It is used to restore the previous rotation after the gameObject has been moved with the camera.
        //!
        Quaternion childrotationBuffer = Quaternion.identity;


        //!
        //! rotation to use when in editor mode. get set from MouseInput
        //!
        private Quaternion rotationEditor;
        public Quaternion RotationEditor
        {
            get { return rotationEditor; }
            set { rotationEditor = value; }
        }


        //! 
        //! This value holds the compensation values defined through the calibration process and added to each rotaation transform.
        //! The calibration process enables a user to define a custom null direction (divergent to the magnitic north)
        //!
        Vector3 rotationCompensation = Vector3.zero;

        //settings & variables for frames-per-second display

        //!
        //! update interval for fps display in seconds
        //!
        private float updateInterval = 0.5F;
        //!
        //! enable / disable fps display
        //!
        public bool showFPS = true;
        //!
        //! string to be displayed in fps display, updated regulary
        //!
        private string fpsText = "";
        //!
        //! FPS accumulated over the interval
        //!
        private float accum = 0;
        //!
        //! Frames drawn over the interval
        //!
        private int frames = 0;
        //!
        //! Left time for current interval
        //!
        private float timeleft;

        //smooth Translation variables

        //!
        //! slow down factor for smooth translation
        //!
        private float translationDamping = 1.0f;
        //!
        //! final target position of current translation
        //!
        private Vector3 targetTranslation = Vector3.zero;
        //!
        //! enable / disable smooth translation
        //!
        private bool smoothTranslationActive = false;
        //!
        //! Time since the last smooth translation of the camera has been started.
        //! Used to terminate the smooth translation after 3 seconds.
        //!
        private float smoothTranslateTime = 0;

        //update sending parameters

        //!
        //! maximum update interval for server communication in seconds
        //!
        static private float updateIntervall = 1.0f / 30.0f;
        //!
        //! last server update time
        //!
        private float lastUpdateTime = -1;
        //!
        //! position of attached object at last server update
        //! used to track movement
        //!
        private Vector3 lastPosition = Vector3.zero;

        //!
        //! 
        //!
        public bool doApplyRotation = true;
#if USE_TANGO
        private Transform tangoTransform;
#endif
        private Transform cameraParent;
        private GyroAdapter gyroAdapter;
        private bool firstApplyTransform = true;
        private Quaternion rotationOffset = Quaternion.identity;
        private Quaternion rotationFirst = Quaternion.identity;
        private Vector3 positionOffset = Vector3.zero;
        private Vector3 positionFirst = Vector3.zero;

		//! Make certain camera / DOF parameters accessible via setter / getter

		//! set / get aperture (DOF component)
		public float Aperture // camera aperture (DOF component)
		{
			set { this.GetComponent<DepthOfField>().aperture = value; }
			get { return this.GetComponent<DepthOfField>().aperture; }
		}
		
		//! set / get camera field of view (vertical)
		public float Fov 
		{
			set { this.GetComponent<Camera>().fieldOfView = value; }
			get { return this.GetComponent<Camera>().fieldOfView; }
		}
		
		//! set / get focal distance (DOF component)
		public float focDist // focal distance in world space (DOF component) 
		{
			set { this.GetComponent<DepthOfField>().focalLength = value; }
			get { return this.GetComponent<DepthOfField>().focalLength; }
		}
		
		//! set / get focal size (DOF component)
		public float focSize
		{
			set { this.GetComponent<DepthOfField>().focalSize = value; }
			get { return this.GetComponent<DepthOfField>().focalSize; }
		}
		
		//! set / get focus visualization status (DOF component)
		public bool visualizeFocus 
		{
			set { this.GetComponent<DepthOfField>().visualizeFocus = value; }
			get { return this.GetComponent<DepthOfField>().visualizeFocus; }
		}
		
		//! set / get status of depth of field component
		public bool depthOfField
		{
			set { this.GetComponent<DepthOfField>().enabled = value; }
			get { return this.GetComponent<DepthOfField>().enabled; }
		}


        void Awake()
        {
            gyroAdapter = new GyroAdapter();
            cameraParent = this.transform.parent;
        }


        //!
        //! Use this for initialization
        //!
        void Start()
        {
            timeleft = updateInterval;
            //initialize the sensor reading for the current platform
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
	        initalizeSensorReading();
#elif (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
	        // SensorHelper.ActivateRotation();
#endif

            //sync renderInFront camera to mainCamera
            Camera frontCamera = this.transform.GetChild(0).GetComponent<Camera>();
            if (frontCamera)
            {
                frontCamera.fieldOfView = this.GetComponent<Camera>().fieldOfView;
                frontCamera.farClipPlane = this.GetComponent<Camera>().farClipPlane;
                frontCamera.nearClipPlane = this.GetComponent<Camera>().nearClipPlane;
            }

            //sync Outline camera to mainCamera
            if (frontCamera.transform.childCount > 0)
            {
                Camera outlineCamera = frontCamera.transform.GetChild(0).GetComponent<Camera>();
                outlineCamera.fieldOfView = this.GetComponent<Camera>().fieldOfView;
                outlineCamera.farClipPlane = this.GetComponent<Camera>().farClipPlane;
                outlineCamera.nearClipPlane = this.GetComponent<Camera>().nearClipPlane;
            }

            // get server adapter
            GameObject refObject = GameObject.Find("ServerAdapter");
            if (refObject != null) serverAdapter = refObject.GetComponent<ServerAdapter>();
            if (serverAdapter == null) Debug.LogError(string.Format("{0}: No ServerAdapter found.", this.GetType()));

            // get mainController
            refObject = GameObject.Find("MainController");
            if (refObject != null) mainController = refObject.GetComponent<MainController>();
            if (mainController == null) Debug.LogError(string.Format("{0}: No MainController found.", this.GetType()));

#if USE_TANGO
                tangoTransform = GameObject.Find("Tango").transform;
#else
            Camera.main.transform.parent.transform.Rotate(Vector3.right, 90);
#endif
        }

        //!
        //! Update is called once per frame
        //!
        void Update()
        {
            //forward changes of the fov to the secondary (render in front) camera
            Camera frontCamera = this.transform.GetChild(0).GetComponent<Camera>();
            frontCamera.fieldOfView = this.GetComponent<Camera>().fieldOfView;

            //forward changes of the fov to the third (overlay) camera
            if (frontCamera.transform.childCount > 0)
            {
                Camera outlineCamera = frontCamera.transform.GetChild(0).GetComponent<Camera>();
                outlineCamera.fieldOfView = this.GetComponent<Camera>().fieldOfView;
            }

            //get sensor data from native Plugin on Windows
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
	        Marshal.Copy(getOrientationSensorData(), orientationSensorData, 0, 4);
#endif

            if (move)
            {
                //cache rotation of attached gameObjects
                if (this.transform.childCount > 1)
                {
                    childrotationBuffer = this.transform.GetChild(1).rotation;
                }

                Quaternion newRotation = Quaternion.identity;
                Vector3 newPosition = Vector3.zero;
#if !UNITY_EDITOR
#if (UNITY_ANDROID || UNITY_IOS)
#if USE_TANGO
                newRotation = tangoTransform.rotation;
                newPosition = tangoTransform.position;
#else
                newRotation = gyroAdapter.Rotation;
#endif
#elif UNITY_STANDALONE_WIN
                newRotation = Quaternion.Euler(90,90,0) * convertRotation(new Quaternion(orientationSensorData[0], orientationSensorData[1], orientationSensorData[2], orientationSensorData[3]));
#endif
#endif
                if (doApplyRotation)
                {
                    if (!firstApplyTransform)
                    {
                        rotationOffset = rotationFirst * Quaternion.Inverse(newRotation);
                        positionOffset = positionFirst - newPosition;
                        firstApplyTransform = true;
                    }
                    //grab sensor reading on current platform
#if !UNITY_EDITOR
#if (UNITY_ANDROID || UNITY_IOS) && !USE_TANGO
                    transform.localRotation = rotationOffset * Quaternion.Euler(0,0,55) * newRotation ;
#elif UNITY_STANDALONE_WIN
                    transform.rotation = rotationOffset * newRotation;
#else
                    transform.rotation = rotationOffset * Quaternion.Euler(0,95,0) * newRotation ;
#if USE_TANGO
                    cameraParent.position = positionOffset + newPosition;              
#endif
#endif
#endif
                }
                else if (firstApplyTransform)
                {
                    rotationFirst = rotationOffset * newRotation;
                    positionFirst = positionOffset + newPosition;
                    firstApplyTransform = false;
                }

                //reset rotation of attached gameObjects and send update to server if neccessary 
                if (this.transform.childCount > 1)
                {
                    this.transform.GetChild(1).rotation = childrotationBuffer;
                    if (this.transform.GetChild(1).position != lastPosition)
                    {
                        //only sends updates every 30 times per second (at most)
                        if ((Time.time - lastUpdateTime) >= updateIntervall)
                        {
                            lastUpdateTime = Time.time;
                            lastPosition = this.transform.GetChild(1).position;
							serverAdapter.sendTranslation(this.transform.GetChild(1) );
                        }
                    }
                }
            }

            //smoothly "fly" the camera to a given position
            if (smoothTranslationActive)
            {
                transform.position = Vector3.Lerp(transform.position, targetTranslation, Time.deltaTime * translationDamping);
                //if the position is nearly reached, stop
                if (Vector3.Distance(transform.position, targetTranslation) < 0.0001f)
                {
                    transform.position = targetTranslation;
                    smoothTranslationActive = false;
                }
                //if 3 seconds have past, stop (avoids infinit translation for unreachable points)
                if ((Time.time - smoothTranslateTime) > 3.0f)
                {
                    smoothTranslationActive = false;
                }
            }

            //calculate & display frames per second
            if (VPETSettings.Instance.debugMsg)
            {
                timeleft -= Time.deltaTime;
                accum += Time.timeScale / Time.deltaTime;
                ++frames;

                // Interval ended - update GUI text and start new interval
                if (timeleft <= 0.0)
                {
                    // display two digits
                    float fps = accum / frames;
                    string format = System.String.Format("{0:F2} FPS", fps);
                    fpsText = format;
                    fpsText += " LiveView IP: " + VPETSettings.Instance.serverIP;
                    fpsText += " State: " + mainController.ActiveMode.ToString();
                    fpsText += " DeviceType: " + SystemInfo.deviceType.ToString();
                    fpsText += " DeviceName: " + SystemInfo.deviceName.ToString();
                    fpsText += " DeviceModel: " + SystemInfo.deviceModel.ToString();
                    fpsText += " SupportGyro: " + SystemInfo.supportsGyroscope.ToString();
                    fpsText += " DataPath: " + Application.dataPath;
                    fpsText += " PersistPath: " + Application.persistentDataPath;
                    fpsText += " Config1: " + Application.dataPath + "/VPET/editing_tool.cfg";
                    fpsText += " Config2: " + Application.persistentDataPath + "/editing_tool.cfg";
                    fpsText += " Mouse Active: " + mainController.MouseInputActive;
                    fpsText += " Touch Active: " + mainController.TouchInputActive;
                    fpsText += " Msg: " + VPETSettings.Instance.msg;
                    accum = 0.0F;
                    frames = 0;
                }
            }

        }

        public void resetCameraOffset()
        {
            rotationOffset = Quaternion.identity;
        }

        //!
        //! initalize a smooth translation of the camera to a given point
        //! @param    position    world position to send the camera to
        //!
        public void smoothTranslate(Vector3 position)
        {
            smoothTranslateTime = Time.time;
            smoothTranslationActive = true;
            targetTranslation = position;
        }

        //!
        //! converts a quaternion from right handed to left handed system
        //! @param    q    right handed quaternion
        //! @return   left handed quaternion
        //!
        private static Quaternion convertRotation(Quaternion q)
        {
            return new Quaternion(q.x, q.y, -q.z, -q.w);
        }

        //!
        //! GUI draw call
        //!
        void OnGUI()
        {
            if (VPETSettings.Instance.debugMsg)
            {
                GUI.Label(new Rect(10, 10, 800, 200), fpsText);
            }
        }

        //!
        //! Setter function for the move variable.
        //! Enables / Disables sensor based, automatic camera rotation.
        //! @param    set     sensor based camera movement on/off    
        //!
        public void setMove(bool set)
        {
            move = set;

            // HACK TANGO
            mainController.setTangoActive(set);

        }

        //!
        //! Calibrates the camera and enables a user to define a custom null direction (divergent to the magnitic north).
        //! The new null direction will be the tablet rotation at execution time.
        //! Only the y axis is affected.
        //!
        public void calibrate()
        {
            if (this.transform.rotation.eulerAngles.y >= 0)
            {
                rotationCompensation.y = 180 - (this.transform.rotation.eulerAngles.y - rotationCompensation.y % 360);
            }
            else
            {
                rotationCompensation.y = 180 + (this.transform.rotation.eulerAngles.y - rotationCompensation.y % 360);
            }
        }

        public void calibrate(float angleY)
        {
            if (this.transform.rotation.eulerAngles.y >= 0)
            {
                rotationCompensation.y = 180 - (angleY - rotationCompensation.y % 360);
            }
            else
            {
                rotationCompensation.y = 180 + (angleY - rotationCompensation.y % 360);
            }
        }

    }
}