// JoyShockLibrary.h - Contains declarations of functions
#pragma once

#if _MSC_VER // this is defined when compiling with Visual Studio
#define JOY_SHOCK_API __declspec(dllexport) // Visual Studio needs annotating exported functions with this
#else
#define JOY_SHOCK_API // XCode does not need annotating exported functions, so define is empty
#endif

#define JS_TYPE_JOYCON_LEFT 1
#define JS_TYPE_JOYCON_RIGHT 2
#define JS_TYPE_PRO_CONTROLLER 3
#define JS_TYPE_DS4 4
#define JS_TYPE_DS 5

#define JS_SPLIT_TYPE_LEFT 1
#define JS_SPLIT_TYPE_RIGHT 2
#define JS_SPLIT_TYPE_FULL 3

#define JSMASK_UP 0x00001
#define JSMASK_DOWN 0x00002
#define JSMASK_LEFT 0x00004
#define JSMASK_RIGHT 0x00008
#define JSMASK_PLUS 0x00010
#define JSMASK_OPTIONS 0x00010
#define JSMASK_MINUS 0x00020
#define JSMASK_SHARE 0x00020
#define JSMASK_LCLICK 0x00040
#define JSMASK_RCLICK 0x00080
#define JSMASK_L 0x00100
#define JSMASK_R 0x00200
#define JSMASK_ZL 0x00400
#define JSMASK_ZR 0x00800
#define JSMASK_S 0x01000
#define JSMASK_E 0x02000
#define JSMASK_W 0x04000
#define JSMASK_N 0x08000
#define JSMASK_HOME 0x10000
#define JSMASK_PS 0x10000
#define JSMASK_CAPTURE 0x20000
#define JSMASK_TOUCHPAD_CLICK 0x20000
#define JSMASK_MIC 0x40000
#define JSMASK_SL 0x40000
#define JSMASK_SR 0x80000

#define JSOFFSET_UP 0
#define JSOFFSET_DOWN 1
#define JSOFFSET_LEFT 2
#define JSOFFSET_RIGHT 3
#define JSOFFSET_PLUS 4
#define JSOFFSET_OPTIONS 4
#define JSOFFSET_MINUS 5
#define JSOFFSET_SHARE 5
#define JSOFFSET_LCLICK 6
#define JSOFFSET_RCLICK 7
#define JSOFFSET_L 8
#define JSOFFSET_R 9
#define JSOFFSET_ZL 10
#define JSOFFSET_ZR 11
#define JSOFFSET_S 12
#define JSOFFSET_E 13
#define JSOFFSET_W 14
#define JSOFFSET_N 15
#define JSOFFSET_HOME 16
#define JSOFFSET_PS 16
#define JSOFFSET_CAPTURE 17
#define JSOFFSET_TOUCHPAD_CLICK 17
#define JSOFFSET_MIC 18
#define JSOFFSET_SL 18
#define JSOFFSET_SR 19

// PS5 Player maps for the DS Player Lightbar
#define DS5_PLAYER_1 4
#define DS5_PLAYER_2 10
#define DS5_PLAYER_3 21
#define DS5_PLAYER_4 27
#define DS5_PLAYER_5 31

typedef struct JOY_SHOCK_STATE {
	int buttons = 0;
	float lTrigger = 0.f;
	float rTrigger = 0.f;
	float stickLX = 0.f;
	float stickLY = 0.f;
	float stickRX = 0.f;
	float stickRY = 0.f;
} JOY_SHOCK_STATE;

typedef struct IMU_STATE {
	float accelX = 0.f;
	float accelY = 0.f;
	float accelZ = 0.f;
	float gyroX = 0.f;
	float gyroY = 0.f;
	float gyroZ = 0.f;
} IMU_STATE;

typedef struct MOTION_STATE {
	float quatW = 0.f;
	float quatX = 0.f;
	float quatY = 0.f;
	float quatZ = 0.f;
	float accelX = 0.f;
	float accelY = 0.f;
	float accelZ = 0.f;
	float gravX = 0.f;
	float gravY = 0.f;
	float gravZ = 0.f;
} MOTION_STATE;

typedef struct TOUCH_STATE {
	int t0Id = 0;
	int t1Id = 0;
	bool t0Down = false;
	bool t1Down = false;
	float t0X = 0.f;
	float t0Y = 0.f;
	float t1X = 0.f;
	float t1Y = 0.f;
} TOUCH_STATE;

typedef struct JSL_AUTO_CALIBRATION {
	float confidence = 0.f;
	bool autoCalibrationEnabled = false;
	bool isSteady = false;
} JSL_AUTO_CALIBRATION;

typedef struct JSL_SETTINGS {
	int gyroSpace = 0;
	int colour = 0;
	int playerNumber = 0;
	int controllerType = 0;
	int splitType = 0;
	bool isCalibrating = false;
	bool autoCalibrationEnabled = false;
	bool isConnected = false;
} JSL_SETTINGS;

extern "C" JOY_SHOCK_API int JslConnectDevices();
extern "C" JOY_SHOCK_API int JslGetConnectedDeviceHandles(int* deviceHandleArray, int size);
extern "C" JOY_SHOCK_API void JslDisconnectAndDisposeAll();
extern "C" JOY_SHOCK_API bool JslStillConnected(int deviceId);

// get buttons as bits in the following order, using North South East West to name face buttons to avoid ambiguity between Xbox and Nintendo layouts:
// 0x00001: up
// 0x00002: down
// 0x00004: left
// 0x00008: right
// 0x00010: plus
// 0x00020: minus
// 0x00040: left stick click
// 0x00080: right stick click
// 0x00100: L
// 0x00200: R
// ZL and ZR are reported as analogue inputs (GetLeftTrigger, GetRightTrigger), because DS4 and XBox controllers use analogue triggers, but we also have them as raw buttons
// 0x00400: ZL
// 0x00800: ZR
// 0x01000: S
// 0x02000: E
// 0x04000: W
// 0x08000: N
// 0x10000: home / PS
// 0x20000: capture / touchpad-click
// 0x40000: SL
// 0x80000: SR
// These are the best way to get all the buttons/triggers/sticks, gyro/accelerometer (IMU), orientation/acceleration/gravity (Motion), or touchpad
extern "C" JOY_SHOCK_API JOY_SHOCK_STATE JslGetSimpleState(int deviceId);
extern "C" JOY_SHOCK_API IMU_STATE JslGetIMUState(int deviceId);
extern "C" JOY_SHOCK_API MOTION_STATE JslGetMotionState(int deviceId);
extern "C" JOY_SHOCK_API TOUCH_STATE JslGetTouchState(int deviceId, bool previous = false);
extern "C" JOY_SHOCK_API bool JslGetTouchpadDimension(int deviceId, int &sizeX, int &sizeY);

extern "C" JOY_SHOCK_API int JslGetButtons(int deviceId);

// get thumbsticks
extern "C" JOY_SHOCK_API float JslGetLeftX(int deviceId);
extern "C" JOY_SHOCK_API float JslGetLeftY(int deviceId);
extern "C" JOY_SHOCK_API float JslGetRightX(int deviceId);
extern "C" JOY_SHOCK_API float JslGetRightY(int deviceId);

// get triggers. Switch controllers don't have analogue triggers, but will report 0.0 or 1.0 so they can be used in the same way as others
extern "C" JOY_SHOCK_API float JslGetLeftTrigger(int deviceId);
extern "C" JOY_SHOCK_API float JslGetRightTrigger(int deviceId);

// get gyro
extern "C" JOY_SHOCK_API float JslGetGyroX(int deviceId);
extern "C" JOY_SHOCK_API float JslGetGyroY(int deviceId);
extern "C" JOY_SHOCK_API float JslGetGyroZ(int deviceId);

// get accumulated average gyro since this function was last called or last flushed values
extern "C" JOY_SHOCK_API void JslGetAndFlushAccumulatedGyro(int deviceId, float& gyroX, float& gyroY, float& gyroZ);

// set gyro space. JslGetGyro*, JslGetAndFlushAccumulatedGyro, JslGetIMUState, and the IMU_STATEs reported in the callback functions will use one of 3 transformations:
// 0 = local space -> no transformation is done on gyro input
// 1 = world space -> gyro input is transformed based on the calculated gravity direction to account for the player's preferred controller orientation
// 2 = player space -> a simple combination of local and world space that is as adaptive as world space but is as robust as local space
extern "C" JOY_SHOCK_API void JslSetGyroSpace(int deviceId, int gyroSpace);

// get accelerometor
extern "C" JOY_SHOCK_API float JslGetAccelX(int deviceId);
extern "C" JOY_SHOCK_API float JslGetAccelY(int deviceId);
extern "C" JOY_SHOCK_API float JslGetAccelZ(int deviceId);

// get touchpad
extern "C" JOY_SHOCK_API int JslGetTouchId(int deviceId, bool secondTouch = false);
extern "C" JOY_SHOCK_API bool JslGetTouchDown(int deviceId, bool secondTouch = false);

extern "C" JOY_SHOCK_API float JslGetTouchX(int deviceId, bool secondTouch = false);
extern "C" JOY_SHOCK_API float JslGetTouchY(int deviceId, bool secondTouch = false);

// analog parameters have different resolutions depending on device
extern "C" JOY_SHOCK_API float JslGetStickStep(int deviceId);
extern "C" JOY_SHOCK_API float JslGetTriggerStep(int deviceId);
extern "C" JOY_SHOCK_API float JslGetPollRate(int deviceId);
extern "C" JOY_SHOCK_API float JslGetTimeSinceLastUpdate(int deviceId);

// calibration
extern "C" JOY_SHOCK_API void JslResetContinuousCalibration(int deviceId);
extern "C" JOY_SHOCK_API void JslStartContinuousCalibration(int deviceId);
extern "C" JOY_SHOCK_API void JslPauseContinuousCalibration(int deviceId);
extern "C" JOY_SHOCK_API void JslSetAutomaticCalibration(int deviceId, bool enabled);
extern "C" JOY_SHOCK_API void JslGetCalibrationOffset(int deviceId, float& xOffset, float& yOffset, float& zOffset);
extern "C" JOY_SHOCK_API void JslSetCalibrationOffset(int deviceId, float xOffset, float yOffset, float zOffset);
extern "C" JOY_SHOCK_API JSL_AUTO_CALIBRATION JslGetAutoCalibrationStatus(int deviceId);

// this function will get called for each input event from each controller
extern "C" JOY_SHOCK_API void JslSetCallback(void(*callback)(int, JOY_SHOCK_STATE, JOY_SHOCK_STATE, IMU_STATE, IMU_STATE, float));
// this function will get called for each input event, even if touch data didn't update
extern "C" JOY_SHOCK_API void JslSetTouchCallback(void(*callback)(int, TOUCH_STATE, TOUCH_STATE, float));
// this function will get called for each device when it is newly connected
extern "C" JOY_SHOCK_API void JslSetConnectCallback(void(*callback)(int));
// this function will get called for each device when it is disconnected
extern "C" JOY_SHOCK_API void JslSetDisconnectCallback(void(*callback)(int, bool));

// super-getter for reading a whole lot of state at once
extern "C" JOY_SHOCK_API JSL_SETTINGS JslGetControllerInfoAndSettings(int deviceId);
// what kind of controller is this?
extern "C" JOY_SHOCK_API int JslGetControllerType(int deviceId);
// is this a left, right, or full controller?
extern "C" JOY_SHOCK_API int JslGetControllerSplitType(int deviceId);
// what colour is the controller (not all controllers support this; those that don't will report white)
extern "C" JOY_SHOCK_API int JslGetControllerColour(int deviceId);
// set controller light colour (not all controllers have a light whose colour can be set, but that just means nothing will be done when this is called -- no harm)
extern "C" JOY_SHOCK_API void JslSetLightColour(int deviceId, int colour);
// set controller rumble
extern "C" JOY_SHOCK_API void JslSetRumble(int deviceId, int smallRumble, int bigRumble);
// set controller player number indicator (not all controllers have a number indicator which can be set, but that just means nothing will be done when this is called -- no harm)
extern "C" JOY_SHOCK_API void JslSetPlayerNumber(int deviceId, int number);
