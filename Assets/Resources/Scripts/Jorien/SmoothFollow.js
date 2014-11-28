/*
This camera smoothes out rotation around the y-axis and height.
Horizontal Distance to the target is always fixed.

There are many different ways to smooth the rotation but doing it this way gives you a lot of control over how the camera behaves.

For every of those smoothed values we calculate the wanted value and the current value.
Then we smooth it using the Lerp function.
Then we apply the smoothed values to the transform's position.
*/

// The target we are following
var target : Transform;
var targetHeight = 0.00;
// The distance in the x-z plane to the target
var distance = 10.0;
// the height we want the camera to be above the target
var height = 5.0;
// How much we 
var heightDamping = 2.0;
var rotationDamping = 3.0;
var turnFromInput = 0.00;

// Place the script in the Camera-Control group in the component menu
@AddComponentMenu("Camera-Control/Smooth Follow")
partial class SmoothFollow { }


function LateUpdate () {
	if (!target)
		return;
	
	// Calculate the current rotation angles
	var wantedRotationAngle;
	if (target.rigidbody.velocity.magnitude < 0.01) {
		wantedRotationAngle = transform.eulerAngles.y;
	}
	else {
		wantedRotationAngle = Quaternion.LookRotation(target.rigidbody.velocity + target.transform.forward).eulerAngles.y;
	}
	wantedRotationAngle += Input.GetAxis("Horizontal") * turnFromInput;
	
	wantedHeight = target.position.y + height;
		
	currentRotationAngle = transform.eulerAngles.y;
	currentHeight = transform.position.y;
	
	// Damp the rotation around the y-axis
	currentRotationAngle = Mathf.LerpAngle (currentRotationAngle, wantedRotationAngle, rotationDamping * Time.deltaTime);

	// Damp the height
	currentHeight = Mathf.Lerp (currentHeight, wantedHeight, heightDamping * Time.deltaTime);

	// Convert the angle into a rotation
	// The quaternion interface uses radians not degrees so we need to convert from degrees to radians
	currentRotation = Quaternion.Euler(0, currentRotationAngle, 0);
	
	// Set the position of the camera on the x-z plane to:
	// distance meters behind the target
	pos = target.position;
	pos.y += targetHeight;
	transform.position = pos;
	transform.position -= currentRotation * Vector3.forward * distance;

	// Set the height of the camera
	transform.position.y = currentHeight;
	
	// Always look at the target
	transform.LookAt (pos);
}
