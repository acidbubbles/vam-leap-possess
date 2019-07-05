using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using SimpleJSON;
using System.Linq;
using Leap;
using Leap.Unity;

namespace ExtraltodeusPlugin {
	public class LeapPossess : MVRScript {
		LeapProvider provider;
		Leap.Frame frame;
		List<Hand> hands;

		Leap.Vector h;
		Leap.Vector rv;
		LeapQuaternion r;

		FreeControllerV3 lh;
		FreeControllerV3 rh;
		FreeControllerV3 hd;

		GenerateDAZMorphsControlUI morphControl;
		DAZMorph morph;

		protected JSONStorableFloat xOffsetSlider;
		protected JSONStorableFloat headDistance;

		Camera mainCamera;

		GenerateDAZMorphsControlUI returnMorphs() {
			JSONStorable geometry = containingAtom.GetStorableByID("geometry");
			DAZCharacterSelector character = geometry as DAZCharacterSelector;
			return character.morphsControlUI;
		}

		string[] leftFingers = {
			"Left Thumb Bend",
			"Left Index Finger Bend",
			"Left Mid Finger Bend",
			"Left Ring Finger Bend",
			"Left Pinky Finger Bend"
		};
		string[] rightFingers = {
			"Right Thumb Bend",
			"Right Index Finger Bend",
			"Right Mid Finger Bend",
			"Right Ring Finger Bend",
			"Right Pinky Finger Bend"
		};

		public override void Init() {
			try {
				headDistance = new JSONStorableFloat("Camera to head distance required", 0.2f, 0f, 0.5f, true);
				headDistance.storeType = JSONStorableParam.StoreType.Full;
				RegisterFloat(headDistance);
				CreateSlider(headDistance, false);

				xOffsetSlider = new JSONStorableFloat("X Offset", 0.045f, -0.1f, 0.1f, true);
				xOffsetSlider.storeType = JSONStorableParam.StoreType.Full;
				RegisterFloat(xOffsetSlider);
				CreateSlider(xOffsetSlider, false);

				provider = FindObjectOfType<LeapProvider>() as LeapProvider;
				frame = provider.CurrentFrame;
				hands = frame.Hands;




				if (SuperController.singleton.isOVR) {
						mainCamera = SuperController.singleton.ViveCenterCamera;
				} else {
						mainCamera = SuperController.singleton.lookCamera;
				}

				JSONStorableFloat cameraRecess = new JSONStorableFloat("Camera Recess", 0.0f, 0f, .2f, true);
				UIDynamicSlider recessSlider = CreateSlider(cameraRecess);
				recessSlider.slider.onValueChanged.AddListener(delegate (float val) {
						Possessor possessor = SuperController.FindObjectsOfType(typeof(Possessor)).Where(p => p.name == "CenterEye").Select(p => p as Possessor).First();
						//possessor is linked to cameras position so hold possessor still while moving camera
						Vector3 pos = possessor.transform.position;
						mainCamera.transform.position = pos - mainCamera.transform.rotation * Vector3.forward * val;
						possessor.transform.position = pos;
				});

				JSONStorableFloat clipDistance = new JSONStorableFloat("Clip Distance", 0.01f, 0.01f, .2f, true);
				UIDynamicSlider clipSlider = CreateSlider(clipDistance);
				clipSlider.slider.onValueChanged.AddListener(delegate (float val) {
						mainCamera.nearClipPlane = val;
				});

				UIDynamicButton applyClip = CreateButton("Camera clip preset", true);
				applyClip.button.onClick.AddListener(delegate (){
					cameraRecess.val = 0.06f;
					clipDistance.val = 0.08f;
				});


			}
			catch (Exception e) {
				SuperController.LogError("Exception caught: " + e);
			}
		}

		void Start() {
			try {
				morphControl = returnMorphs();
				
				morphControl.GetMorphByDisplayName("Left Thumb Fist").morphValue = 0;
				morphControl.GetMorphByDisplayName("Right Thumb Fist").morphValue = 0;
				morphControl.GetMorphByDisplayName("Left Fingers Fist").morphValue = 0;
				morphControl.GetMorphByDisplayName("Right Fingers Fist").morphValue = 0;

				lh = containingAtom.GetStorableByID("lHandControl") as FreeControllerV3;
				rh = containingAtom.GetStorableByID("rHandControl") as FreeControllerV3;
				hd = containingAtom.GetStorableByID("headControl")  as FreeControllerV3;
			}
			catch (Exception e) {
				SuperController.LogError("Exception caught: " + e);
			}
		}

		protected float minMax(float minV, float maxV, float curVal) {
			float result = curVal * (maxV - minV) + minV ;
			if (result > maxV)
				result = maxV;
			if (result < minV)
					result = minV;
			return result;
		}

		Quaternion leapQuaternionToQuaternion(LeapQuaternion lq) {
			return new Quaternion(lq.x,lq.y,lq.z,lq.w);
		}

		Vector3 leapVectorToVector3(Leap.Vector v) {
			return new Vector3(v[0],v[1],v[2]);
		}

		Quaternion vector3ToQuaternion(Vector3 v) {
			return Quaternion.Euler(v.x, v.y, v.z);
		}

		float correctRotation(float x, float offset) {
			if (x < offset)
				x *= -1;
			return x;
		}

		float positiveAttitude(float x) {
			if (x < 0)
				x *= -1;
			return x;
		}

		float getHeadDistance() {
			Vector3 camPos  = mainCamera.transform.position;
			Vector3 headPos = hd.transform.position;

			return Vector3.Distance(camPos, headPos);
		}

		private void animateHands(FreeControllerV3 hc, Hand hand, string[] fingerMorphs, float mirror) {
			float fx;

			h = hand.PalmPosition;
			r = hand.Rotation;

			Vector3 pos = new Vector3(h[0],h[1],h[2]);
			Quaternion rot = new Quaternion(r.x,r.y,r.z,r.w);

			hc.transform.position = pos;
			hc.transform.rotation = rot;
			hc.transform.Rotate(0,90*mirror,0);
			hc.transform.Translate(xOffsetSlider.val*mirror, 0, 0, Space.Self);

			for (int i=0; i< hand.Fingers.Count; i++) {
				DAZMorph morph = morphControl.GetMorphByDisplayName(fingerMorphs[i]);
				Quaternion vd = leapQuaternionToQuaternion(hand.Fingers[i].bones[3].Rotation);
				vd = Quaternion.Inverse(vd * Quaternion.Euler(0, -90*mirror, 0)) * rot;

				if (i == 0){
					fx = (0.5f-minMax(0f,1f,positiveAttitude(vd.x)))*1.22f;
				}else{
					fx = minMax(0f,1f,correctRotation(vd.x, 0.65f))*0.9f;
				}
				if (fx != 0)
					morph.morphValue = fx/0.85f;
			}
		}

		void FixedUpdate() {
			try {
				if (getHeadDistance() <= headDistance.val){
					foreach (Hand hand in hands){
						if (hand.IsLeft){
							animateHands(lh, hand, leftFingers,  1);
						}else{
							animateHands(rh, hand, rightFingers, -1);
						}
					}
				}
			}
			catch (Exception e) {
				SuperController.LogError("Exception caught: " + e);
			}
		}
	}
}
