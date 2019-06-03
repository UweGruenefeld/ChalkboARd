/**
 * ChalkboARD
 *
 * This script controls the AR scene 
 *
 * @file Controller.cs
 * @author Uwe Gruenefeld, Torge Wolff, Niklas Diekmann
 * @version 2019-02-01
 **/
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Mime;
using System.Numerics;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Vuforia;
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;

public class Controller : MonoBehaviour
{
    public GameObject Camera;
    //The black plane in front of the camera
    public GameObject Plane;
    //the target for projecting the model in the scene
    public GameObject Target;
    //our model that we want to projecting on the target
    public GameObject Figure;
    // The backplane on which the camera image is rendered
    public GameObject BackPlane;
    
    //Enum that displays the single Steps in the process
    private float _timer;
    private float _planeOpeningSpeed;
    private float _backPlaneZ;
    private float _backPlaneX;
    private float _backPlaneY;
    private readonly float _initPlaneOpeningSpeed = 0.3f;
    private readonly float _waitInIdle = 30.0f;
    private bool _firstEntryOfState;
    private bool _prodMode;
    private int _runCounter;
    private Animator _animator;
    private AnimatorClipInfo[] _clipInfo;
    private Camera _cam;
    private State _currentState;
    private Vector3 _oldPosition;
    private Vector3 _oldFigureScale = Vector3.zero;
    private Vector3 _planePosition = Vector3.zero;
    private Vector3 _figurePosition = Vector3.zero;
    private Vector3 _figureScale = Vector3.zero;
    private DateTime _lastFatherInput;
    
    //TODO - we need this for window effect!
    private Vector3 _backPlanePosition = Vector3.zero;
    private readonly Vector3 _scalingVector = new Vector3(0.5f,0.5f,0.5f);
    
    private void Start()
    {
        Debug.Log("Start.");
        
        //setting the starting state
        _currentState = State.INIT;
        _firstEntryOfState = true;
        _runCounter = 1;
        _planeOpeningSpeed = _initPlaneOpeningSpeed;
        
        //getting the animator controller
        _animator = Figure.GetComponent<Animator>();
        _animator.Play("anim_default");

        //getting Camera Component from Vuforia Camera
        _cam = Camera.GetComponent<Camera>();
        
        CreateCleanState();
    }

    void Update()
    {
        #region Window-Effekt Controls
        #region JoyStick Axis Control
        //Debug.Log("X-Achse: " + Input.GetAxis("Horizontal") + ", Y-Achse: " + Input.GetAxis("Vertical"));
        DateTime currentTime = DateTime.Now;
        if (Input.GetAxis("Vertical") > 0.8 && currentTime.Subtract(_lastFatherInput).TotalMilliseconds > 10)
        {
            _lastFatherInput = currentTime;
            MoveBackplaneUp();
        }
        if (Input.GetAxis("Vertical") < -0.8 && currentTime.Subtract(_lastFatherInput).TotalMilliseconds > 10)
        {
            _lastFatherInput = currentTime;
            MoveBackplaneDown();
        }
        if (Input.GetAxis("Horizontal") > 0.8 && currentTime.Subtract(_lastFatherInput).TotalMilliseconds > 10)
        {
            _lastFatherInput = currentTime;
            MoveBackplaneRight();
        }
        if (Input.GetAxis("Horizontal") < -0.8 && currentTime.Subtract(_lastFatherInput).TotalMilliseconds > 10)
        {
            _lastFatherInput = currentTime;
            MoveBackplaneLeft();
        }
        #endregion
        #region Button Controls
        if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.JoystickButton3))
        {
            ZoomInBackplane();
        }
        if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.JoystickButton2))
        {
            ZoomOutBackplane();
        }
        else if (Input.GetKey(KeyCode.W))
        {
            MoveBackplaneUp();
        }
        else if (Input.GetKey(KeyCode.S))
        {
            Debug.Log("Moving Down.");
            MoveBackplaneDown();
        }
        else if (Input.GetKey(KeyCode.A))
        {
            MoveBackplaneLeft();
        }
        else if (Input.GetKey(KeyCode.D))
        {
            MoveBackplaneRight();
        }
        #endregion
        #endregion

        if (Input.GetKeyDown("space") || Input.GetKeyDown(KeyCode.JoystickButton0)) // Input.GetKeyDown(KeyCode.Space) || Input.GetButtonDown("Submit")
        {
            _prodMode = !_prodMode;
        }
        
        switch (_currentState)
        {
            //Init State. Figure does nothing, but hanging on plane.
            case State.INIT:
                InitSession();
                break;
            
            case State.IDLE:
                FigureIdle();
                break;
            
            // State of figure falling down on the bottom of the screen.
            case State.FIGURE_FALLING:
                FigureFalling();
                break;

            // State of figure standing up after falling down.
            case State.FIGURE_LANDING:
                FigureLanding();
                break;
            
            // State of window plane moved up/ out of camera view.
            case State.WINDOW_OPENING:
                WindowOpening();
                break;

            // State of figure looking before jumping.
            case State.FIGURE_LOOKING:
                FigureLooking();
                break;

            // State of figure jumping on image target.
            case State.FIGURE_JUMPING:
                FigureJumping();
                break;

            //State of figure doing party on image target.
            case State.FIGURE_PARTY:
                FigureParty();
                break;

            //State of figure leaving field of view of camera.
            case State.FIGURE_LEAVING:
                FigureLeaving();
                break;
            
            //State of Window closing. Plane moving down in field of view.
            case State.WINDOW_CLOSING:
                WindowClosing();
                break;
        }
    }

    #region Window Effekt Methods
    private void ZoomInBackplane()
    {
        Debug.Log("Zoomin in Backplane");
        _backPlaneZ = BackPlane.GetComponent<Transform>().localPosition.z - 5.0f;
        BackPlane.GetComponent<Transform>().transform.localPosition =
            new Vector3(BackPlane.GetComponent<Transform>().localPosition.x,
                BackPlane.GetComponent<Transform>().localPosition.y, _backPlaneZ);
    }

    private void ZoomOutBackplane()
    {
        Debug.Log("Zoomin out Backplane");
        _backPlaneZ = BackPlane.GetComponent<Transform>().localPosition.z + 5.0f;
        BackPlane.GetComponent<Transform>().transform.localPosition =
            new Vector3(BackPlane.GetComponent<Transform>().localPosition.x,
                BackPlane.GetComponent<Transform>().localPosition.y, _backPlaneZ);
    }

    private void MoveBackplaneRight()
    {
        Debug.Log("Moving Backplane right");
        _backPlaneX = BackPlane.GetComponent<Transform>().localPosition.x - 5.0f;
        BackPlane.GetComponent<Transform>().transform.localPosition =
            new Vector3(_backPlaneX,
                BackPlane.GetComponent<Transform>().localPosition.y, BackPlane.GetComponent<Transform>().localPosition.z);
    }

    private void MoveBackplaneLeft()
    {
        Debug.Log("Moving Backplane left");
        _backPlaneX = BackPlane.GetComponent<Transform>().localPosition.x + 5.0f;
        BackPlane.GetComponent<Transform>().transform.localPosition =
            new Vector3(_backPlaneX,
                BackPlane.GetComponent<Transform>().localPosition.y, BackPlane.GetComponent<Transform>().localPosition.z);
    }

    private void MoveBackplaneUp()
    {
        Debug.Log("Moving Backplane up");
        _backPlaneY = BackPlane.GetComponent<Transform>().localPosition.y - 5.0f;
        BackPlane.GetComponent<Transform>().transform.localPosition =
            new Vector3(BackPlane.GetComponent<Transform>().localPosition.x,
                _backPlaneY, BackPlane.GetComponent<Transform>().localPosition.z);
    }

    private void MoveBackplaneDown()
    {
        Debug.Log("Moving Backplane down");
        _backPlaneY = BackPlane.GetComponent<Transform>().localPosition.y + 5.0f;
        BackPlane.GetComponent<Transform>().transform.localPosition =
            new Vector3(BackPlane.GetComponent<Transform>().localPosition.x,
                _backPlaneY, BackPlane.GetComponent<Transform>().localPosition.z);
    }
    #endregion

    private void InitSession()
    {
        FirstEntryOfState();
        WaitForVuforia();
    }
    
    private void FigureIdle()
    {
        FirstEntryOfState();
        StartCamera(false);
        
        //if the user presses space or the submit button on the remote controller, the process is starting
        if (_runCounter > 1 && _prodMode)
        {   // wait for n seconds after each run!
            Debug.Log("_RunCounter: " + _runCounter + ".");

            _timer += Time.deltaTime;
            
            if (_timer >= _waitInIdle)
            {
                LeaveState(State.FIGURE_FALLING);
            }
        } else if (_prodMode)
        {
            LeaveState(State.FIGURE_FALLING);
        } else if (!_prodMode)
        {
            Debug.Log("Do nothing.");
        }
    }

    private void FigureFalling()
    {
        FirstEntryOfState();
        
        //playing the falling animation while transforming the position of the figure
        _animator.Play("anim_falling");
        
        // let the figure only fall until the bottom of the screen
        if (_cam.WorldToScreenPoint(Figure.transform.position).x < _cam.pixelWidth)
        {
            //calculating the position of the figure
            _figurePosition = Figure.transform.localPosition;
            _figurePosition.x -= Time.deltaTime * Mathf.Sqrt(Mathf.Abs(_figurePosition.x) + .1f) * 5f;
            Figure.transform.localPosition = _figurePosition;
        }
        else
        {
            StartCamera(true);
            Figure.transform.parent = null;
            LeaveState(State.FIGURE_LANDING);
        }
    }

    
    private void FigureLanding()
    {
        FirstEntryOfState();
        
        _animator.Play("anim_landing");
        //getting info about the standing up information
        _clipInfo = _animator.GetCurrentAnimatorClipInfo(0);
        _timer += Time.deltaTime;

        //waiting till the animation is over and then switching to the next state
        if (_timer >= _clipInfo[0].clip.length)
        {
            _timer = 0;
            if (_runCounter == 1)
            {
                Debug.Log("Scale Backplane!");
                //Get Background Plane and scale for Window Effect!
                BackPlane = GameObject.Find("BackgroundPlane");
                _backPlanePosition  = BackPlane.transform.localPosition;
                _backPlanePosition.z -= 400.0f;
                _backPlanePosition.x -= 220;
                BackPlane.transform.localPosition= _backPlanePosition;
            }
            _runCounter += 1;
            LeaveState(State.WINDOW_OPENING);
        }
    }
    
    private void WindowOpening()
    {
        FirstEntryOfState();
        
        //Figure opening plane.
        _animator.Play("anim_bending");
        _clipInfo = _animator.GetCurrentAnimatorClipInfo(0);
        _timer += Time.deltaTime;
            
        if (_timer >= 0.5f)
        {
            OpenPlane();
        }

        //Condition to change state.
        if (_planePosition.y > 2 && _timer >= _clipInfo[0].clip.length)
        {
            _oldPosition = Figure.transform.position;
            _planeOpeningSpeed = _initPlaneOpeningSpeed;
            LeaveState(State.FIGURE_LOOKING);
        }
    }

    
    private void FigureLooking()
    {
        _timer += Time.deltaTime;

        if (_timer >= 1.5f)
            _oldFigureScale = Figure.transform.localScale;
            _figureScale = _oldFigureScale +  new Vector3(0.01f,0.01f,0.01f);
            LeaveState(State.FIGURE_JUMPING);
    }

    
    private void FigureJumping()
    {
        FirstEntryOfState();
        _animator.Play("anim_jumping");
        _timer += Time.deltaTime;
        _timer = Mathf.Min(1, _timer);
        _figurePosition = Vector3.Lerp(_oldPosition, Target.transform.position, _timer);
        Figure.transform.position = _figurePosition;
        //Figure.transform.localScale = Vector3.Lerp(_oldFigureScale, _figureScale, _timer);

        //Condition to change state.
        if (_timer >= 1)
        {
            Figure.transform.parent = Target.transform;
            Figure.transform.localPosition = Vector3.zero;
            LeaveState(State.FIGURE_PARTY);
        }
    }

    
    private void FigureParty()
    {
        FirstEntryOfState();

        _timer += Time.deltaTime;

        if(_timer >= 1.5f)
            _animator.Play("anim_dancing");

        //Condition to change state.
        if (_timer >= 17.0f)
        {
            LeaveState(State.FIGURE_LEAVING);
        }
    }

    
    private void FigureLeaving()
    {
        FirstEntryOfState();

        _timer += Time.deltaTime;
        _timer = Mathf.Min(.1f, _timer);

        Vector3 rotation = Vector3.Slerp(Target.transform.rotation.eulerAngles, 
        Target.transform.rotation.eulerAngles + new Vector3(0, 145, 0), _timer * 10);
        Figure.transform.rotation = Quaternion.Euler(rotation.x, rotation.y, rotation.z);
        
        _animator.Play("anim_walking");
        _figurePosition = Figure.transform.localPosition;
        _figurePosition.z += Time.deltaTime * 0.5f;
        Figure.transform.localPosition = _figurePosition;

        //Condition to change state.
        if (_figurePosition.z > 1.5f)
        {
            LeaveState(State.WINDOW_CLOSING);
        }
    }

    
    private void WindowClosing()
    {
       FirstEntryOfState();
        
       ClosePlane();
        
        if (_planePosition.y < 0)
        {
            _oldPosition = Figure.transform.position;
            LeaveState(State.IDLE);
        }
    }

    private void StartCamera(bool start)
    {
         
        if (start && !VuforiaBehaviour.Instance.enabled)
        {
            VuforiaBehaviour.Instance.enabled = true;
            CameraDevice.Instance.Start();
        }
        else if(VuforiaBehaviour.Instance.enabled)
        {
            VuforiaBehaviour.Instance.enabled = false;
            CameraDevice.Instance.Stop();

        }
           
    }

    
    private void LeaveState(State nextState)
    {
        _timer = 0;
        _firstEntryOfState = true;
        _currentState = nextState;
    }

    
    private void FirstEntryOfState()
    {
        if (_firstEntryOfState)
        {
            Debug.Log(_currentState);
            _firstEntryOfState = false;
        }
    }

    
    private void WaitForVuforia()
    {
        if (VuforiaBehaviour.Instance.enabled)
        {
            _currentState = State.IDLE;
            _firstEntryOfState = true;
        }
    }

    
    private void OpenPlane()
    {
        //_planeOpeningSpeed = _planeOpeningSpeed * 1.02f;
        _planePosition = Plane.transform.position;
        _planePosition.y += Time.deltaTime * Mathf.Sqrt(Mathf.Abs(_planePosition.y) + .1f) * 1.6f; // _planeOpeningSpeed;
        Plane.transform.position = _planePosition;
    }

    
    private void ClosePlane()
    {
        _animator.Play("anim_default");
        
        CreateCleanState();

        //calculating the position change for the plane in front of the camera
        _planePosition = Plane.transform.position;
        _planePosition.y -= Time.deltaTime * 1f;
        Plane.transform.position = _planePosition;
    }

    private void CreateCleanState()
    {
        Figure.transform.parent = Plane.transform;
        Figure.transform.localScale = new Vector3(.25f, .25f, .25f);
        Figure.transform.localPosition = new Vector3(0, .6f, -.15f);
        Figure.transform.rotation = Quaternion.Euler(0, 0, 0);
    }
}
