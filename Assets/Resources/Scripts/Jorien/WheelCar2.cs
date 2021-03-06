using UnityEngine;
using System.Collections;

public enum wheelDrive {
	Front = 0,
	Back = 1,
	All = 2
}


public class WheelCar2 : MonoBehaviour { 

	//Wielen introduceren
	public Transform wheelFR; 
	public Transform wheelFL; 
	public Transform wheelBR; 
	public Transform wheelBL; 
	
	//eigenschappen van de wielen
	public float suspensionDistance = 0.2f; 
	public float springs = 1000.0f; 
	public float dampers = 2f; 
	public float wheelRadius = 0.25f; 
	public float torque = 100f; 
	public float brakeTorque = 500f; 
	public float wheelWeight = 3f;
	public Vector3 shiftCentre = new Vector3(0.0f, -0.5f, 0.0f); 
	public float fwdStiffness = 0.1f; //stijfheid wielen wordt slip mee bepaald
	public float swyStiffness = 0.1f; 	
	public float maxSteerAngle = 30.0f; 
	public wheelDrive wheelDrive = wheelDrive.Front; 
	
	//Schakel voorwaarden
	public float shiftDownRPM = 1500.0f; 
	public float shiftUpRPM = 2500.0f; 
	public float[] gears = { -10f, 9f, 6f, 4.5f, 3f, 2.5f };
	float[] efficiencyTable = { 0.6f, 0.65f, 0.7f, 0.75f, 0.8f, 0.85f, 0.9f, 1.0f, 1.0f, 0.95f, 0.80f, 0.70f, 0.60f, 0.5f, 0.45f, 0.40f, 0.36f, 0.33f, 0.30f, 0.20f, 0.10f, 0.05f };
	float efficiencyTableStep = 250.0f;
	int currentGear = 1; 

	private bool anyOnGround;
	private float curvedSpeedFactor;
	private bool reversing;
	public float SpeedFactor { get;  private set; }
	private float maxReversingSpeed;
	private float maxSpeed = 60;
	public float reversingSpeedFactor = 0.3f; 
	public float downForce=30;
	private float CurrentSpeed;
	
	// alle info van de wielen wordt hierin opgeslagen
	class WheelData {
		public Transform transform;
		public GameObject go;
		public WheelCollider col;
		public Vector3 startPos;
		public Vector3 startRot;
		public float rotation = 0.0f;
		public float maxSteer;
		public bool motor;


	};

	//Er wordt een array aangemaakt waar per wiel data instaat
	WheelData[] wheels; 

	//Hier worden de eigenschappen aan de wiel gegeven en collider aangemaakt
	WheelData SetWheelParams(Transform wheel, float maxSteer, bool motor) {
		WheelData result = new WheelData(); // the container of wheel specific data
		result.startRot = wheel.localRotation.eulerAngles;

		GameObject go = new GameObject("WheelCollider");
		go.transform.parent = transform; // the car, not the wheel is parent
		go.transform.position = wheel.position; // match wheel pos
		WheelCollider col = (WheelCollider) go.AddComponent(typeof(WheelCollider));
		col.motorTorque = 0.0f;
		// Nu wordt alles in Wheeldata gezet
		result.transform = wheel;  
		result.go = go; 
		result.col = col; 
		result.startPos = go.transform.localPosition; // store the current local pos of wheel
		result.maxSteer = maxSteer; 
		result.motor = motor; 
		return result; 
	}
	

	void Start () {
		//Eerst auto goed zwaartepunt geven
		rigidbody.centerOfMass += shiftCentre;
		maxReversingSpeed = maxSpeed * reversingSpeedFactor;


		//De wielen in een WheelData array plaatsen en de settings maken
		wheels = new WheelData[4];		
		bool frontDrive = (wheelDrive == wheelDrive.Front) || (wheelDrive == wheelDrive.All);
		bool backDrive = (wheelDrive == wheelDrive.Back) || (wheelDrive == wheelDrive.All);	
		wheels[0] = SetWheelParams(wheelFR, maxSteerAngle, frontDrive);
		wheels[1] = SetWheelParams(wheelFL, maxSteerAngle, frontDrive);
		wheels[2] = SetWheelParams(wheelBR, 0.0f, backDrive);
		wheels[3] = SetWheelParams(wheelBL, 0.0f, backDrive);
		
		//Na gegevens aan de wielen gegeven te hebben en colliders aangemaakt te hebben nog wat laatste aanpassingen
		foreach (WheelData w in wheels) {
			WheelCollider col = w.col;
			col.suspensionDistance = suspensionDistance;
			JointSpring js = col.suspensionSpring;
			js.spring = springs;
			js.damper = dampers;            
			col.suspensionSpring = js;
			col.radius = wheelRadius;
			col.mass = wheelWeight;

			WheelFrictionCurve fc = col.forwardFriction;
			fc.asymptoteValue = 5000.0f;
			fc.extremumSlip = 2.0f;
			fc.asymptoteSlip = 20.0f;
			fc.stiffness = fwdStiffness;
			col.forwardFriction = fc;
			fc = col.sidewaysFriction;
			fc.asymptoteValue = 7500.0f;
			fc.asymptoteSlip = 2.0f;
			fc.stiffness = swyStiffness;
			col.sidewaysFriction = fc;
			w.col=col; //zelf toegevoegd
		}



	}
	
	float shiftDelay = 0.0f;
	
	//Omhoogschakelen
	public void ShiftUp() {
		float now = Time.timeSinceLevelLoad;
		// Er moet lang genoeg gewacht worden tot de shift
		if (now < shiftDelay) return;
		// Er kan alleen geschakeld worden als de hoogste nog niet bereikt is
		if (currentGear < gears.Length - 1) {
			currentGear ++;
			shiftDelay = now + 1.0f; // De volgende schakeling wordt vertraagd met 1 seconde.
		}
	}
	
	//Omlaagschakelen
	public void ShiftDown() {
		float now = Time.timeSinceLevelLoad;
		// Er moet lang genoeg gewacht worden tot de shift
		if (now < shiftDelay) return;
		// Er kan alleen geschakeld worden als de laagste nog niet bereikt is
		if (currentGear > 0) {
			currentGear --;
			shiftDelay = now + 0.1f; // De volgende schakeling wordt vertraagd met 0.1 seconde, want naar beneden schakelen moet sneller gebeuren.
		}
	}

	float CurveFactor (float factor)
	{
		return 1 - (1 - factor)*(1 - factor);
	}



	void ApplyDownforce ()
	{
		// apply downforce
		if (anyOnGround) {
			rigidbody.AddForce (-transform.up * curvedSpeedFactor * downForce);
		}
	}
	
	float wantedRPM = 0.0f; //Het toerental wat de motor probeert te bereiken
	float motorRPM = 0.0f; //Het toerental van de motor

	

	void FixedUpdate () {
		float delta = Time.fixedDeltaTime;
		
		float steer = 0; // sturen
		float accel = 0; // versnellen
		bool brake = false; // remmen
		steer = Input.GetAxis("Horizontal");
		accel = Input.GetAxis("Vertical");
		brake = Input.GetButton("Jump");

		//Schakelen
		if ((currentGear == 1) && (accel < 0.0f)) { 
			ShiftDown(); //Bij negatieve versnelling naar gear=0 schakelen die heeft negatieve snelheid.
			reversing = true;
		}
		else if ((currentGear == 0) && (accel > 0.0f)) {
			ShiftUp(); //Bij positieve versnelling en gear=0 doorschakelen naar gear=1.
			reversing=false;
		}
		else if ((motorRPM > shiftUpRPM) && (accel > 0.0f)) {
			ShiftUp(); //Als de toerenteller van de moter te hoog wordt, doorschakelen.
			reversing=false;
		}
		else if ((motorRPM < shiftDownRPM) && (currentGear > 1)) { //alleen als gear groter dan 1 is want 0 is achteruit.
			ShiftDown(); //Als de toerenteller van de motor te laag wordt, doorschakelen.
			reversing=false;
		}
		if ((currentGear == 0)) {
			accel = - accel; //Zodat de versnelling negatief wordt
		}
		if (accel < 0.0f) {// Bij negatieve versnelling wordt er geremd.
			brake = true;
			accel = 0.0f;
			wantedRPM = 0.0f;
			reversing=true;
		}
		
		wantedRPM = (5500.0f * accel) * 0.1f + wantedRPM * 0.9f; //Het toerental wat we willen bereiken
		
		float rpm = 0.0f;
		int motorizedWheels = 0;
		bool floorContact = false;

		CurrentSpeed = transform.InverseTransformDirection (rigidbody.velocity).z;
		SpeedFactor = Mathf.InverseLerp (0, reversing ? maxReversingSpeed : maxSpeed, Mathf.Abs (CurrentSpeed));
		curvedSpeedFactor = reversing ? 0 : CurveFactor (SpeedFactor);

		// Toerental van de wielen berekenen
		foreach (WheelData w in wheels) {
			WheelHit hit;
			WheelCollider col = w.col;
			if(col.isGrounded){
				anyOnGround=true;
			}
			if (w.motor) {
				rpm += col.rpm;
				motorizedWheels++;
			}
			
			// Ga de locale rotatie na en zet nieuwe locale rotatie terug met delta 
			w.rotation = Mathf.Repeat(w.rotation + delta * col.rpm * 360.0f / 60.0f, 360.0f);

			// w.transform.rotation = Quaternion.Euler(w.rotation, col.steerAngle, 0.0f);

            w.transform.localRotation = Quaternion.Euler(w.startRot.x + w.rotation , w.startRot.y + col.steerAngle, w.startRot.z);
			
			// zorgt dat de wielen de grond raken
			Vector3 lp = w.transform.localPosition;
			if (col.GetGroundHit(out hit)) {
				lp.y -= Vector3.Dot(w.transform.position - hit.point, transform.up) - col.radius;
				floorContact = floorContact || (w.motor);
			}
			else {
				lp.y = w.startPos.y - suspensionDistance;
			}
			w.transform.localPosition = lp;
		}
		// Toerental berkenen, onafhankelijk van gear.
		if (motorizedWheels > 1) {
			rpm = rpm / motorizedWheels;
		}
		
		// Toerental afhankelijk maken van gear.
		motorRPM = 0.95f * motorRPM + 0.05f * Mathf.Abs(rpm * gears[currentGear]);
		if (motorRPM > 5500.0f) motorRPM = 5500.0f; //Checken of we niet maximum hebben bereikt.
		
		// Efficiecy nagaan met behulp van de tabel gegeven bovenin.
		int index = (int) (motorRPM / efficiencyTableStep);
		if (index >= efficiencyTable.Length) index = efficiencyTable.Length - 1;
		if (index < 0) index = 0;
		
		// torque berekenen met gear en de tabel en daarna de torque aan de wielen geven
		float newTorque = torque * gears[currentGear] * efficiencyTable[index];
		foreach (WheelData w in wheels) {
			WheelCollider col = w.col;
			if (w.motor) {
				// Alleen torque geven als het wiel slomer toerental heeft dan de verwachte
				if (Mathf.Abs(col.rpm) > Mathf.Abs(wantedRPM)) {
					col.motorTorque = 0;
				}
				else {
					float curTorque = col.motorTorque;
					col.motorTorque = curTorque * 0.9f + newTorque * 0.1f;
				}
			}
			// Nagaan of er geremd moet worden en draaihoek definieren van de wielen.
			col.brakeTorque = (brake)?brakeTorque:0.0f;
			col.steerAngle = steer * w.maxSteer;
		}
		

	}
	

}

