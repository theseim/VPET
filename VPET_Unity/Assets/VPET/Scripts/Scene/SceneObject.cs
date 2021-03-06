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
﻿using UnityEngine;
using System.Collections;

//! 
//! This script should be applied to any gameObject that is able to move in the scene, is selectable by the user and should receive all physics (collisions & gravity).
//!
namespace vpet
{
	public class SceneObject : MonoBehaviour {

		//!
		//! cached reference to animation controller
		//!
		private AnimationController animationController;

		//!
		//! cached reference to animation data (runtime representation)
		//!
		private AnimationData animData = null;

		//!
		//! current animation layer
		//!
		public int animationLayer = -1;

		//!
		//! duration of the animation in seconds
		//!
		public float animationDuration = -1;

		//!
		//! curves for the current animation (currently playing or to be played)
		//!
		public AnimationCurve[] transCurves = new AnimationCurve[3];

		//!
		//! curves for the current animation (currently playing or to be played)
		//!
		public AnimationCurve[] rotationCurves = new AnimationCurve[4];

		//!
		//! curves for the current animation (currently playing or to be played)
		//!
		public AnimationCurve[] scaleCurves = new AnimationCurve[3];

		//!
		//! should the animation be looping
		//!
		public bool animationLooping = false;

		//!
		//! maximum update interval for server communication in seconds
		//!
		static private float updateIntervall = 1.0f / 30.0f;
		//!
		//! is the translation update delayed since the last update was just recently
		//!
		bool translationUpdateDelayed = false;
		//!
		//! is the rotation update delayed since the last update was just recently
		//!
		bool rotationUpdateDelayed = false;
		//!
		//! last server update time of translation
		//!
		float lastTranslationUpdateTime = -1;
		//!
		//! number of frames after which the object is asumed as steady (no more physically driven translation changes)
		//!
		int translationStillFrameCount = 11;
		//!
		//! last server update time of rotation
		//!
		float lastRotationUpdateTime = -1;
		//!
		//! number of frames after which the object is asumed as steady (no more physically driven rotation changes)
		//!
		public int rotationStillFrameCount = 11;
		//!
		//! position of object at last server update
		//! used to track movement
		//!
		Vector3 lastPosition;
		//!
		//! rotation of object at last server update
		//! used to track rotation change
		//!
		Quaternion lastRotation;

		//!
		//! should a boxcollider be generated auomatically
		//!
		public bool generateBoxCollider = true;

		//!
		//! cached reference to mainController
		//!
		MainController mainController;
		//!
		//! cached reference to serverAdapter
		//!
		ServerAdapter serverAdapter;
		//!
		//! cached reference to undoRedoController
		//!
		UndoRedoController undoRedoController;

		//!
		//! lock auto detect movement & rotation
		//!
		public bool locked = false;

		//!
		//! lock for one frame (to avoid resending of incoming messages)
		//!
		public bool tempLock = false;

		//!
		//! allow changes to isKinematic property
		//!
		public bool lockKinematic = false;

		//smooth Translation variables

		//!
		//! slow down factor for smooth translation
		//!
		public float translationDamping = 1.0f;
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

		//store intial values for reset

		//!
		//! initial position at startup, used to reset object
		//!
		public Vector3 initialPosition;
		//!
		//! initial rotation at startup, used to reset object
		//!
		public Quaternion initialRotation;
		//!
		//! initial scale at startup, used to reset object
		//!
		public Vector3 initialScale;
		//!
		//! initial light color at startup, used to reset object (only used when object has a light component)
		//!
		public Color initialLightColor;
		//!
		//! initial light intensity at startup, used to reset object (only used when object has a light component)
		//!
		public float initialLightIntensity;
		//!
		//! initial light range at startup, used to reset object (only used when object has a light component)
		//!
		public float initialLightRange;
		//!
		//! initial light spot angle at startup, used to reset object (only used when object has a light component)
		//!
		public float initialSpotAngle;

		//!
		//! is the object currently selected
		//!
		public bool selected = false;
		//!
		//! should the slection visualization be drawn now
		//!
		private bool drawGlowAgain = true;

		//!
		//! enumeration of available light parameters
		//!
		private enum LightParameter {Intensity, Color, Range, Angle};
		//!
		//! last modified light parameter
		//!
		LightParameter lastModifiedLightParameter;

		//!
		//! extends of the object
		//!
		public Bounds bounds;

		//!
		//! target receiving all modifications (e.g. for light -> parent)
		//!
		Transform target;

		//!
		//! is this GameObject a directional light
		//!
		public bool isDirectionalLight = false;
		//!
		//! is this GameObject a spot light
		//!
		public bool isSpotLight = false;
		//!
		//! is this GameObject a point light
		//!
		public bool isPointLight = false;

		public OutlineEffect outlineEffect;
		public Outline outline;

		private Transform lightTarget = null;

		private Transform lightGeo = null;

		private Light sourceLight = null;

		public float exposure = 3f;

		public bool IsLight
		{
			get {
				if (sourceLight != null)
					return true;
				else
					return false;
			}
		}


		//!
		//! Use this for initialization
		//!
		void Start () 
		{
			if (transform.childCount > 0)
			{
				lightTarget = transform.GetChild(0);
				if (lightTarget != null)
				{
					sourceLight = lightTarget.GetComponent<Light>();
				}
			}

			// if (this.transform.parent.transform.GetComponent<Light>())

			if ( sourceLight )
			{

				// target = this.transform.parent;
				target = this.transform;

				initialLightColor = sourceLight.color;
				initialLightColor.a = 0.25f;
				initialLightIntensity = sourceLight.intensity;

				if (sourceLight.type == LightType.Directional)
				{
					isDirectionalLight = true;
					lightGeo = lightTarget.FindChild("Arrow");
				}
				else if (sourceLight.type == LightType.Spot)
				{
					isSpotLight = true;
					initialLightRange = sourceLight.range;
					initialSpotAngle = sourceLight.spotAngle;
					lightGeo = lightTarget.FindChild("Cone");
				}
				else if (sourceLight.type == LightType.Point)
				{
					isPointLight = true;
					initialLightRange = sourceLight.range;
					lightGeo = lightTarget.FindChild("Sphere");
				}


			}
			else
			{
				target = this.transform;
			}

			//initalize cached references
			animationController = GameObject.Find("AnimationController").GetComponent<AnimationController>();
			mainController = GameObject.Find("MainController").GetComponent<MainController>();
			serverAdapter = GameObject.Find("ServerAdapter").GetComponent<ServerAdapter>();
			undoRedoController = GameObject.Find ("UndoRedoController").GetComponent<UndoRedoController> ();

			//cache Reference to animation data
			animData = AnimationData.Data;

			//update initial parameters
			initialPosition = target.position;
			initialRotation = target.rotation;
			initialScale = target.localScale;

			lastPosition = initialPosition;
			lastRotation = initialRotation;


			//generate colliding volumes
			if(generateBoxCollider)
			{


				//calculate bounds
				//target.position = new Vector3(-3000,-3000,-3000);
				//target.localScale = new Vector3(1, 1, 1);
				bounds = new Bounds( Vector3.zero, Vector3.zero );
				//set rotation to 0
				// this.transform.rotation = Quaternion.Euler(Vector3.zero);


				bool hasBounds = false;
				Renderer[] renderers = this.gameObject.GetComponentsInChildren<Renderer>();
				foreach (Renderer render in renderers)
				{
					if (hasBounds)
					{
						bounds.Encapsulate(render.bounds);
					}
					else
					{
						bounds = render.bounds;
						hasBounds = true;
					}
				}

				BoxCollider col = this.gameObject.AddComponent<BoxCollider>();

				if (sourceLight)
				{
					// BoxCollider col_lightquad = lightTarget.FindChild("LightQuad").GetComponent<BoxCollider>();
					// col.size = col_lightquad.size;
					// col.center = col_lightquad.center;
					LightIcon iconScript = lightTarget.FindChild("LightQuad").GetComponent<LightIcon>();
					iconScript.TargetCollider = col;

					iconScript.TargetScale = target.localScale;

				}
				else
				{
					col.center = new Vector3((bounds.center.x - this.transform.position.x) / this.transform.lossyScale.x, (bounds.center.y - this.transform.position.y) / this.transform.lossyScale.y, (bounds.center.z - this.transform.position.z) / this.transform.lossyScale.z);
					col.size = new Vector3(bounds.size.x / this.transform.lossyScale.x, bounds.size.y / this.transform.lossyScale.y, bounds.size.z / this.transform.lossyScale.z);
					col.material = new PhysicMaterial();
					col.material.bounciness = 1.0f;
				}

				//target.position = initialPosition;
				// target.localScale = initialScale;
				// target.rotation = initialRotation;

			}
			if ( !isDirectionalLight && !isPointLight && !isSpotLight && this.name != "camera" )
			{
				this.gameObject.AddComponent<Rigidbody>();
				this.gameObject.GetComponent<Rigidbody>().mass = 100.0f;
				this.gameObject.GetComponent<Rigidbody>().drag = 2.5f;



				// this.gameObject.GetComponent<Rigidbody>().useGravity = false;



			}

			//Initalize animation loading if animation available
			AnimationSerializer asScript =  target.gameObject.AddComponent<AnimationSerializer>();

			asScript.loadData();
			animationController.setStartEndTimeline( asScript.timeStart, asScript.timeEnd );


			updateAnimationCurves();

		}

		//!
		//! Use this for delayed initalization
		//!
		void StartXXX()
		{
			//Initalize animation loading if animation available
			target.gameObject.AddComponent<AnimationSerializer>();
			target.gameObject.GetComponent<AnimationSerializer>().loadData();
			updateAnimationCurves();
		}

		//!
		//! Update is called once per frame
		//!
		void Update () 
		{
			if (lastPosition != target.position)
			{
				lastPosition = target.position;
				translationStillFrameCount = 0;
			}
			else if (translationStillFrameCount < 11)
			{
				translationStillFrameCount++;
			}
			if (lastRotation != target.rotation)
			{
				lastRotation = target.rotation;
				rotationStillFrameCount = 0;
			}
			else if (rotationStillFrameCount < 11)
			{
				rotationStillFrameCount++;
			}
			if (translationStillFrameCount >= 10 && rotationStillFrameCount >= 10 && tempLock)
			{
				tempLock = false;
			}
			if (!locked && !tempLock)
			{
				//publish translation change
				if (mainController.liveMode)
				{
					if (translationStillFrameCount == 0) //position just changed
					{
						if ((Time.time - lastTranslationUpdateTime) >= updateIntervall)
						{
							// serverAdapter.sendTranslation(target, target.position, !selected);
							serverAdapter.sendTranslation(target, !selected );

							serverAdapter.sendTransform( target, !selected );

							lastTranslationUpdateTime = Time.time;
							translationUpdateDelayed = false;
						}
						else
						{
							translationUpdateDelayed = true;
						}
					}
					else if (translationUpdateDelayed) //update delayed, but object not moving
					{
						// serverAdapter.sendTranslation(target, target.position, !selected);
						serverAdapter.sendTranslation(target, !selected );

						serverAdapter.sendTransform( target, !selected );

						lastTranslationUpdateTime = Time.time;
						translationUpdateDelayed = false;
					}
				}
				else if (translationStillFrameCount == 10) //object is now no longer moving
				{
					// serverAdapter.sendTranslation(target, target.position, !selected);
					serverAdapter.sendTranslation(target, !selected );

					serverAdapter.sendTransform( target, !selected );

				}

				//publish rotation change
				if (mainController.liveMode)
				{
					if (rotationStillFrameCount == 0) //position just changed
					{
						if ((Time.time - lastRotationUpdateTime) >= updateIntervall)
						{
							// serverAdapter.sendRotation(target, target.rotation, !selected);
							serverAdapter.sendRotation(target, !selected );

							serverAdapter.sendTransform( target, !selected );

							lastRotationUpdateTime = Time.time;
							rotationUpdateDelayed = false;
						}
						else
						{
							rotationUpdateDelayed = true;
						}
					}
					else if (rotationUpdateDelayed) //update delayed, but object not moving
					{
						//serverAdapter.sendRotation(target, target.rotation, !selected);
						serverAdapter.sendRotation(target, !selected );


						serverAdapter.sendTransform( target, !selected );

						lastRotationUpdateTime = Time.time;
						rotationUpdateDelayed = false;
					}
				}
				else if (rotationStillFrameCount == 10) //object is now no longer moving
				{
					// serverAdapter.sendRotation(target, target.rotation, !selected);
					serverAdapter.sendRotation(target, !selected );

					serverAdapter.sendTransform( target, !selected );

				}

			}

			//turn on highlight modes
			if (selected && drawGlowAgain)
			{
				if (lightGeo)
				{
					lightGeo.GetComponent<Renderer>().enabled = true;
				}
				else
				{
					this.showHighlighted(this.gameObject);
				}
				drawGlowAgain = false;
			}

			//turn off highlight mode
			else if (!selected && !drawGlowAgain)
			{
				if ( lightGeo )
				{
					lightGeo.GetComponent<Renderer>().enabled = false;
				}
				else
				{
					this.showNormal(this.gameObject);
				}
				drawGlowAgain = true;
			}

			//execute smooth translate
			if (smoothTranslationActive)
			{
				target.position = Vector3.Lerp(target.position, targetTranslation, Time.deltaTime * translationDamping);
				if (Vector3.Distance(target.position, targetTranslation) < 0.0001f)
				{
					target.position = targetTranslation;
					smoothTranslationActive = false;
				}
				if ((Time.time - smoothTranslateTime) > 3.0f)
				{
					smoothTranslationActive = false;
					undoRedoController.addAction();
				}
			}
		}


		//!
		//! set the light color of this object, if it is a light
		//!
		public Color getLightColor()
		{
			if (isDirectionalLight || isPointLight || isSpotLight)
			{
				return sourceLight.color;
			}
			return Color.black;
		}

		//!
		//! get the light intensity
		//!
		public float getLightIntensity()
		{
			if (sourceLight != null) return sourceLight.intensity * VPETSettings.Instance.lightIntensityFactor;
			return 0f;
		}


		//!
		//! get the light range
		//!
		public float getLightRange()
		{
			if (isPointLight || isSpotLight)
			{
				return sourceLight.range;
			}
			return float.MaxValue;
		}

		//!
		//! get the light range
		//!
		public float getLightAngle()
		{
			if (isSpotLight)
			{
				return sourceLight.spotAngle;
			}
			return 360f;
		}

		//!
		//! move this object to the floor (the lowest point of the object will be set to have y position = 0)
		//!
		public void moveToFloor()
		{
			target.position = new Vector3(target.position.x, this.GetComponent<BoxCollider>().bounds.size.y / 2, target.position.z);
		}

		//!
		//! rotate object based on arc ball rotation
		//! @param  velocity    velocity (amount) of rotation on given axis
		//! @param  axis        rotation axis
		//!
		public void setArcBallRotation(float velocity, Vector3 axis)
		{
			target.Rotate(axis, velocity, Space.World);
			// if (this.isDirectionalLight || isSpotLight) rotateChilds(velocity, axis);
		}

		//!
		//! reset rotation to values present on startup
		//!
		public void resetRotation()
		{
			target.rotation = initialRotation;
			//serverAdapter.sendRotation(target, target.rotation);
			serverAdapter.sendRotation(target );
			undoRedoController.addAction();
		}

		//!
		//! reset position to values present on startup
		//!
		public void resetPosition()
		{
			target.position = initialPosition;
			//serverAdapter.sendTranslation(target, target.position);
			serverAdapter.sendTranslation(target );

			undoRedoController.addAction();
		}

		//!
		//! reset scale to values present on startup
		//!
		public void resetScale()
		{
			target.localScale = initialScale;
			undoRedoController.addAction();
			//serverAdapter.sendScale(target, target.localScale);
			serverAdapter.sendScale(target );

		}

		//!
		//! reset all parameters to inital values present on startup
		//!
		public void resetAll()
		{
			locked = false;
			serverAdapter.sendLock(this.transform, false);
			target.rotation = initialRotation;
			//serverAdapter.sendRotation(target, target.rotation);
			serverAdapter.sendRotation(target );

			target.position = initialPosition;
			//serverAdapter.sendTranslation(target, target.position);
			serverAdapter.sendTranslation(target );

			target.localScale = initialScale;
			//serverAdapter.sendScale(target, target.localScale);
			serverAdapter.sendScale(target );

			if (isSpotLight || isPointLight || isDirectionalLight) 
			{
				sourceLight.color = initialLightColor;
				lightGeo.GetComponent<Renderer>().material.color = initialLightColor;
				serverAdapter.sendLightColor(target, sourceLight, exposure );
				serverAdapter.sendLightIntensity(target, sourceLight, exposure);
			}
			if (isSpotLight || isPointLight) 
			{
				sourceLight.range = initialLightRange;
				// serverAdapter.sendLightRange(target, initialLightRange);
			}
			if (isSpotLight) 
			{
				sourceLight.spotAngle = initialSpotAngle;
				serverAdapter.sendLightConeAngle(target, sourceLight, exposure);
			}
		}

		//!
		//! rotate childs of this object based on arc ball rotation
		//! @param  velocity    velocity (amount) of rotation on given axis
		//! @param  axis        rotation axis
		//!
		private void rotateChilds(float velocity, Vector3 axis)
		{
			for (int i = 0; i < this.transform.childCount; i++)
			{
				if (!this.transform.GetChild(i).GetComponent<SceneObject>())
				{
					this.transform.GetChild(i).Rotate(axis, -velocity, Space.World);
				}
			}
		}

		//void OnDrawGizmos()
		//{
		//    Collider collider = this.GetComponent<BoxCollider>();
		//    if (collider)
		//    {
		//        Debug.Log(collider.bounds.ToString());
		//        Bounds colliderBounds = collider.bounds;
		//        Gizmos.color = new Color(1, 1, 0, 0.5f);
		//        Gizmos.DrawCube(colliderBounds.center, colliderBounds.size);
		//    }
		//}

		//!
		//! recursively apply highlight shader to object
		//! @param  obj    gameObject on which to apply the highlight shader
		//!
		private void showHighlighted(GameObject obj)
		{
			//do it for parent object
			if (obj.GetComponent<Renderer>() != null) //is object rendered?
			{
				////add makeBrighter Material
				//Material[] newMaterials = new Material[obj.GetComponent<Renderer>().materials.GetLength(0) + 1];
				//obj.GetComponent<Renderer>().materials.CopyTo(newMaterials, 0);
				//newMaterials[newMaterials.GetLength(0) - 1] = Resources.Load("VPET/Materials/makeBrighter", typeof(Material)) as Material;
				//obj.GetComponent<Renderer>().materials = newMaterials;

				if (Camera.main.transform.GetChild(0).GetComponent<OutlineEffect>())
				{
					Outline outline = obj.GetComponent<Outline>();
                    if (!outline)
                        obj.AddComponent<Outline>();
                    else
                        outline.enabled = true;
                        //obj.SetActive(true);
                }
			}
			foreach (Transform child in obj.transform)
			{
				this.showHighlighted(child.gameObject);
			}
		}

		//!
		//! recursively delete highlight shader of object
		//! @param  obj    gameObject on which to delete the highlight shader
		//!
		private void showNormal(GameObject obj)
		{
			//do it for parent object
			if (obj.GetComponent<Renderer>() != null) //is object rendered?
			{
				////remove makeBrighter Material
				//Material[] oldMaterials = new Material[obj.GetComponent<Renderer>().materials.GetLength(0) - 1];
				//for (int i = 0; i < obj.GetComponent<Renderer>().materials.GetLength(0) - 1; i++)
				//{
				//    oldMaterials[i] = obj.GetComponent<Renderer>().materials[i];
				//}
				//obj.GetComponent<Renderer>().materials = oldMaterials;

				Outline outline = obj.GetComponent<Outline>();
                if (outline)
                    outline.enabled = false;

            }
			foreach (Transform child in obj.transform)
			{
				this.showNormal(child.gameObject);
			}
		}

		//!
		//! initalize a smooth translation of the object to a given point
		//! @param    pos    world position to send the object to
		//!
		public void smoothTranslate(Vector3 pos)
		{
			smoothTranslationActive = true;
			smoothTranslateTime = Time.time;
			targetTranslation = pos;
		}


		// TODO: Why local ??

		//!
		//! translate the object immediately
		//! @param    pos    world position to send the object to
		//!
		public void translate(Vector3 pos)
		{
			target.transform.position = pos;
		}


		//!
		//! scale the object immediately
		//! @param    scl    local scale to send the object to
		//!
		public void scale( Vector3 scl )
		{
			target.transform.localScale = scl;

			// HACK
			//serverAdapter.sendScale( target, target.localScale );
			serverAdapter.sendScale(target );


		}


		//!
		//! set the kinematic parameter of the object
		//! @param      set     new value for kinematic
		//! @param      send    should this modification be published to the server     
		//!
		public void setKinematic(bool set, bool send = true)
		{
			if (!lockKinematic && !isDirectionalLight && !isSpotLight && !isPointLight)
			{
				this.gameObject.GetComponent<Rigidbody>().isKinematic = set;
				if (send) serverAdapter.sendKinematic(target, set);
				if (!set)
				{
					this.gameObject.GetComponent<Rigidbody>().WakeUp();
				}
			}
		}

		//!
		//! set the light color of this object, if it is a light
		//! @param      color     new color of the light  
		//!
		public void setLightColor(Color color)
		{
			if (isDirectionalLight || isPointLight || isSpotLight)
			{
				color.a = 0.25f;
				sourceLight.color = color;
				lightGeo.GetComponent<Renderer>().material.color = color;
				lastModifiedLightParameter = LightParameter.Color;
				if (mainController.liveMode)
				{
					serverAdapter.sendLightColor(target, sourceLight, exposure );
				}
			}
		}

		//!
		//! set the light intensity of this object, if it is a light
		//! @param      intensity     new intensity of the light  
		//!
		public void setLightIntensity(float intensity)
		{
			if (isDirectionalLight || isPointLight || isSpotLight)
			{
				sourceLight.intensity = intensity / VPETSettings.Instance.lightIntensityFactor;
				lastModifiedLightParameter = LightParameter.Intensity;
				if (mainController.liveMode)
				{
					serverAdapter.sendLightIntensity(target, sourceLight, exposure );
				}
			}
		}


		//!
		//! set the light range of this object, if it is a light
		//! @param      range     new range of the light  
		//!
		public void setLightRange(float range)
		{
			if (isPointLight || isSpotLight)
			{
				sourceLight.range = range;
				lastModifiedLightParameter = LightParameter.Range;
				if (mainController.liveMode)
				{
					// serverAdapter.sendLightRange(target, light.range);
				}
			}
		}

		//!
		//! set the light delta range of this object, if it is a light
		//! @param      delta     new delta range of the light  
		//!
		public void setLightDeltaRange(float delta)
		{
			if (isPointLight || isSpotLight)
			{
				sourceLight.range += delta;
				lastModifiedLightParameter = LightParameter.Range;
				if (mainController.liveMode)
				{
					// serverAdapter.sendLightRange(target, sourceLight.range);
				}
			}
		}

		//!
		//! set the light cone angle of this object, if it is a light
		//! @param      angle     new cone angle of the light  
		//!
		public void setLightAngle(float angle)
		{
			if (isSpotLight)
			{
				sourceLight.spotAngle = angle;
				lastModifiedLightParameter = LightParameter.Angle;
				if (mainController.liveMode)
				{
					serverAdapter.sendLightConeAngle(target, sourceLight, exposure);
				}
			}
		}

		//!
		//! hide or show the visualization (cone, sphere, arrow) of the light
		//! @param      set     hide-> true, show->false   
		//!
		public void hideLightVisualization(bool set)
		{
			if ( lightGeo )
			{
				lightGeo.GetComponent<Renderer>().enabled = !set;
			}
		}

		//!
		//! send updates of the last modifications to the network server
		//!
		public void sendUpdate()
		{
			if (mainController.ActiveMode == MainController.Mode.scaleMode)
			{
				//serverAdapter.sendScale(target, target.transform.localScale);
				serverAdapter.sendScale(target );

				undoRedoController.addAction();
			}
			if (mainController.ActiveMode == MainController.Mode.lightSettingsMode)
			{
				switch (lastModifiedLightParameter)
				{
				case (LightParameter.Intensity):
					serverAdapter.sendLightIntensity(target, sourceLight, exposure);
					undoRedoController.addAction();
					break;
				case (LightParameter.Color):
					serverAdapter.sendLightColor(target, sourceLight, exposure );
					undoRedoController.addAction();
					break;
				case (LightParameter.Angle):
					serverAdapter.sendLightConeAngle(target, sourceLight, exposure);
					undoRedoController.addAction();
					break;
				case (LightParameter.Range):
					// serverAdapter.sendLightRange(target, sourceLight.range);
					undoRedoController.addAction();
					break;
				default:
					break;
				}
			}
		}
			
	    //!
	    //! set the sceneObjects to a given position (time) in the animation
	    //! @param      time        time within the animation
	    //!
	    public void setAnimationState(float time)
	    {
	        if ( !animationLooping )
	        {
	            this.transform.localPosition = new Vector3( transCurves[0].Evaluate( time ),
	                                                  transCurves[1].Evaluate( time ),
	                                                  transCurves[2].Evaluate( time ) );
	
	            /*
	            this.transform.localRotation = new Quaternion( rotationCurves[0].Evaluate( time ),
	                                                  rotationCurves[1].Evaluate( time ),
	                                                  rotationCurves[2].Evaluate( time ),
	                                                  rotationCurves[3].Evaluate( time ) );
	             */
	        }
	        else
	        {
	            this.transform.localPosition = new Vector3( transCurves[0].Evaluate( time % animationDuration ),
	                                                  transCurves[1].Evaluate( time % animationDuration ),
	                                                  transCurves[2].Evaluate( time % animationDuration ) );
	
	            /*
	            this.transform.localRotation = new Quaternion( rotationCurves[0].Evaluate( time ),
	                                                  rotationCurves[1].Evaluate( time  % animationDuration ),
	                                                  rotationCurves[2].Evaluate( time  % animationDuration ),
	                                                  rotationCurves[3].Evaluate( time % animationDuration ) );
	             */
	        }
	
	
	    }
	
	    //!
	    //! update the animation curves
	    //! recalculate the position of the sceneObject based on the current animation curve & time
	    //! normally called after the animation curve has changed
	    //!
	    public void updateAnimationCurves()
	    {
	        if (animData.getAnimationClips(target.gameObject) != null)
	        {
	            transCurves[0] = animData.getAnimationCurve(animData.getAnimationClips(target.gameObject)[0], "m_LocalPosition.x");
	            transCurves[1] = animData.getAnimationCurve(animData.getAnimationClips(target.gameObject)[0], "m_LocalPosition.y");
	            transCurves[2] = animData.getAnimationCurve(animData.getAnimationClips(target.gameObject)[0], "m_LocalPosition.z");
	
	            rotationCurves[0] = animData.getAnimationCurve( animData.getAnimationClips( target.gameObject )[0], "m_LocalRotation.x" );
	            rotationCurves[1] = animData.getAnimationCurve( animData.getAnimationClips( target.gameObject )[0], "m_LocalRotation.y" );
	            rotationCurves[2] = animData.getAnimationCurve( animData.getAnimationClips( target.gameObject )[0], "m_LocalRotation.z" );
	            rotationCurves[3] = animData.getAnimationCurve( animData.getAnimationClips( target.gameObject )[0], "m_LocalRotation.w" );
	
	            
	            animationDuration = animData.getAnimationClips(target.gameObject)[0].length;
	            setAnimationState(animationController.currentAnimationTime);
	        }
	    }
	
	    //!
	    //! enable/disable looping for the current animation
	    //!
	    public void enableLooping(bool set)
	    {
	        animationLooping = set;
	    }
	
	    //!
	    //! sets the keyframe at the current time of the animation to a new value for the currently animated object
	    //! automatically adds keyframe if there is none at this time of the animation
	    //!
	    public void setKeyframe()
	    {
	        bool movedSuccessfully = false;
	        int keyIndex = -1;
	
	        //x value
	        Keyframe key = new Keyframe(animationController.currentAnimationTime, this.transform.position.x);
	        for (int i = 0; i < this.transCurves[0].length; i++)
	        {
	            if (Mathf.Abs(this.transCurves[0].keys[i].time - animationController.currentAnimationTime) < 0.04)
	            {
	                this.transCurves[0].MoveKey(i, key);
	                keyIndex = i;
	                movedSuccessfully = true;
	            }
	        }
	        if (!movedSuccessfully)
	        {
	            keyIndex = this.transCurves[0].AddKey(key);
	            movedSuccessfully = false;
	        }
	        if (transCurves[0].keys.Length > 1)
	        {
	            if (keyIndex == 0) this.transCurves[0].SmoothTangents(1, 0);
	            if (keyIndex == transCurves[0].keys.Length - 1) this.transCurves[0].SmoothTangents(transCurves[0].keys.Length - 2, 0);
	        }
	        this.transCurves[0].SmoothTangents(keyIndex, 0);
	
	        //y value
	        key = new Keyframe(animationController.currentAnimationTime, this.transform.position.y);
	        for (int i = 0; i < this.transCurves[1].length; i++)
	        {
	            if (Mathf.Abs(this.transCurves[1].keys[i].time - animationController.currentAnimationTime) < 0.04)
	            {
	                this.transCurves[1].MoveKey(i, key);
	                keyIndex = i;
	                movedSuccessfully = true;
	            }
	        }
	        if (!movedSuccessfully)
	        {
	            keyIndex = this.transCurves[1].AddKey(key);
	            movedSuccessfully = false;
	        }
	        if (transCurves[1].keys.Length > 1)
	        {
	            if (keyIndex == 0) this.transCurves[1].SmoothTangents(1, 0);
	            if (keyIndex == transCurves[1].keys.Length - 1) this.transCurves[1].SmoothTangents(transCurves[1].keys.Length - 2, 0);
	        }
	        this.transCurves[1].SmoothTangents(keyIndex, 0);
	
	        //z value
	        key = new Keyframe(animationController.currentAnimationTime, this.transform.position.z);
	        for (int i = 0; i < this.transCurves[2].length; i++)
	        {
	            if (Mathf.Abs(this.transCurves[2].keys[i].time - animationController.currentAnimationTime) < 0.04)
	            {
	                this.transCurves[2].MoveKey(i, key);
	                keyIndex = i;
	                movedSuccessfully = true;
	            }
	        }
	        if (!movedSuccessfully)
	        {
	            keyIndex = this.transCurves[2].AddKey(key);
	            movedSuccessfully = false;
	        }
	
	        if (transCurves[2].keys.Length > 1)
	        {
	            if (keyIndex == 0) this.transCurves[2].SmoothTangents(1, 0);
	            if (keyIndex == transCurves[2].keys.Length - 1) this.transCurves[2].SmoothTangents(transCurves[2].keys.Length - 2, 0);
	        }
	        this.transCurves[2].SmoothTangents(keyIndex, 0);
	
	        animData.changeAnimationCurve(animData.getAnimationClips(target.gameObject)[0], typeof(Transform), "m_LocalPosition.x", transCurves[0]);
	        animData.changeAnimationCurve(animData.getAnimationClips(target.gameObject)[0], typeof(Transform), "m_LocalPosition.y", transCurves[1]);
	        animData.changeAnimationCurve(animData.getAnimationClips(target.gameObject)[0], typeof(Transform), "m_LocalPosition.z", transCurves[2]);
	
	        updateAnimationCurves();
	    }
}
}