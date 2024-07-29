using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Unity.XR.CoreUtils;
using UnityEditor.Media;
using UnityEngine;

public class Heli : MonoBehaviour
{

    public float FoV;


    public float baseFoV = 140f;
    public float vertFov = 100f;
    public float currentFoV;
    // float horVerRatio = 1.839026278f;

    public TextAsset forcingFuncFile;
    public TextAsset trainingFile;
    public TextAsset thetaFile;
    [SerializeField] GameObject Marker;
    private GameObject marker;
    private float markerDist = 5;

    private float[] forcingFunc;
    private float[] trainingFunc1;
    private float[] trainingFunc2;
    private float[] thetaForcingFunc;
    private float dtPython = 0.1f;
    private float T_m = 120.0f;
    private float T_total;

    public float pushValue;
    public float pitchSpeed = 1f;
    private float smoothTime = 0.1f;
    private float maxPitchRate = 5.0f;
    private float maxVal = 1;
    private float angleWanted;
    private float currentPitch;
    private float finalAngle;
    private float newPitch;
    private Vector3 newEuler;
    private float currentAccel;
    private Vector3 controlVelocity;
    float currentTheta;
    private Vector3 ffVelocity;
    private Vector3 ffTheta;
    private float deltaTheta;
    private float Mu = 0.0468f;
    private float Mq = -1.8954f;
    private float M_theta1s = 26.4f;
    private float g = 9.80665f;
    private float X_u = -0.02f;
    private float X_theta1s = -9.280f;

    private List<Data> exportData;
    bool recording = false;
    public bool kill = false;
    public bool move = true;

    private float beginTIme = 0f;
    public Quaternion initialRotation;
    public Vector3 spawnLocation;
    private string indicator;
    public string id;
    int counterCR = 0;
    int counterUp = 0;

// Key mapping
    private KeyCode pitchDown = KeyCode.UpArrow;
    private KeyCode pitchUp = KeyCode.DownArrow;
    private KeyCode reset = KeyCode.R;
    private KeyCode startTraining = KeyCode.T;
    private KeyCode startFF = KeyCode.Space;
    private KeyCode startTheta = KeyCode.P;
    private KeyCode FoV20 = KeyCode.Z;
    private KeyCode FoV30 = KeyCode.X;
    private KeyCode FoV60 = KeyCode.C;
    private KeyCode FoV90 = KeyCode.V;
    private KeyCode FoV120 = KeyCode.B;
    private KeyCode FoV140 = KeyCode.N;
    


    private void SpawnMarker(){
        var markerPos = new Vector3(transform.localPosition.x,transform.localPosition.y,transform.localPosition.z +markerDist);
        if (marker == null){
            marker = Instantiate(Marker, markerPos,transform.rotation);
        }
    }
    public float ConvertToHorFoV(float fov_wanted, Camera cam)
    {
        float ratio = (fov_wanted / 2) / 57.29578f;
        var half_theta = Mathf.Atan(Mathf.Tan(ratio * Mathf.Deg2Rad) / cam.aspect);
        return 2 * half_theta * Mathf.Rad2Deg;
    }

    public void ChangeFoV(float fov)
    {
        var distance = GetComponent<Camera>().nearClipPlane + 0.5f;


        // double length = (Math.Tan(Mathf.Deg2Rad * baseFoV / 2) - Math.Tan(Mathf.Deg2Rad * fov / 2)) * distance;
        Transform blockerRight = transform.Find("BlockerRight");
        double length = blockerRight.transform.localScale.x;
        Debug.Log("Length" + length + " " + distance);
        double pos_x = (Math.Tan(Mathf.Deg2Rad * fov / 2) * distance) + (length / 2);
        Debug.Log("pos_x" + pos_x);

        Vector3 currentSize = blockerRight.transform.localScale;
        Vector3 currentPos = blockerRight.transform.localPosition;
        blockerRight.transform.localScale = new Vector3((float)length, currentSize.y + 4, currentSize.z);
        blockerRight.transform.localPosition = transform.rotation * new Vector3((float)pos_x, currentPos.y, distance);

        Transform blockerLeft = transform.Find("BlockerLeft");
        blockerLeft.transform.localScale = blockerRight.transform.localScale;
        blockerLeft.transform.localPosition = new Vector3((float)-pos_x, currentPos.y, distance);



        if (!blockerRight.gameObject.activeSelf)
        {
            blockerRight.gameObject.SetActive(true);
            blockerLeft.gameObject.SetActive(true);
        }
        currentFoV = fov;


    }

    private void GetData()
    {

        string[] data = forcingFuncFile.text.Split(new string[] { ",", "\n" }, StringSplitOptions.None);
        int tableSize = data.Length / 2 - 1;
        forcingFunc = new float[tableSize];
        for (int i = 0; i < tableSize; i++)
        {
            var value = float.Parse(data[2 * (i + 1) + 1], CultureInfo.InvariantCulture);
            forcingFunc[i] = value;

        }

    }
    private void GetThetaData(){
        string[] data = thetaFile.text.Split(new string[] {",","\n"},StringSplitOptions.None);
        int tableSize = data.Length /2 -1;
        thetaForcingFunc = new float[tableSize];
        for (int i =0; i<tableSize; i++)
        {
            var value = float.Parse(data[2 * (i + 1) + 1], CultureInfo.InvariantCulture);
            thetaForcingFunc[i] = value;
        }

    }
    private void GetTrainingData()
    {

        string[] data = trainingFile.text.Split(new string[] { ",", "\n" }, StringSplitOptions.None);
        int tableSize = data.Length / 3 - 1;
        trainingFunc1 = new float[tableSize];
        trainingFunc2 = new float[tableSize];
        for (int i = 0; i < tableSize; i++)
        {
            var value = float.Parse(data[3 * (i + 1) + 1], CultureInfo.InvariantCulture);
            trainingFunc1[i] = value;
            var value2 = float.Parse(data[3 * (i + 1) + 2]) * Mathf.Rad2Deg;
            trainingFunc2[i] = value2;

        }

    }
    private float GetPitch()
    {
        // if(ffTheta.x>180) ffTheta.x-=360;
        if (transform.rotation.eulerAngles.x > 180f)
        {
            //Debug.Log($"Pitch: {360 - transform.rotation.eulerAngles.x} Rad {(360 - transform.rotation.eulerAngles.x) * Mathf.Deg2Rad}");
            return (360 - transform.rotation.eulerAngles.x) * Mathf.Deg2Rad;
        }
        else
        {
            //Debug.Log($"Pitch: {-transform.rotation.eulerAngles.x} Rad {(-transform.rotation.eulerAngles.x) * Mathf.Deg2Rad}");
            return (-transform.rotation.eulerAngles.x) * Mathf.Deg2Rad;
        }

    }

    IEnumerator ChangeVelocity()
    {
        float elapsedTime = 0f;
        int index = 0;
        float beginTime = Time.time;
        while (elapsedTime < T_total)
        {
            float t = elapsedTime % dtPython / dtPython;
            float currentVelocity = Mathf.Lerp(forcingFunc[index], forcingFunc[(index + 1) % forcingFunc.Length], t);

            ffVelocity = new Vector3(0.0f, 0.0f, currentVelocity);
        float targetTime = beginTime + elapsedTime + dtPython;
        while (Time.time < targetTime)
        {
            yield return null;
        }

        elapsedTime += dtPython;

        index = (index + 1) % forcingFunc.Length;
        Debug.Log($"elapsed time {elapsedTime} actual {Time.time - beginTIme} diff {Time.time - beginTIme -elapsedTime}");
        }
        recording = false;
        SaveToFile();
        kill = true;
        Start();
    }
    IEnumerator ChangeTheta(){
        float elapsedTime = 0f;
        int index = 0;
        var beginTime = Time.time;
        move = false;
        while(elapsedTime < T_total){
            float t = elapsedTime % dtPython / dtPython;
            currentTheta = Mathf.Lerp(thetaForcingFunc[index], thetaForcingFunc[(index+1) % thetaForcingFunc.Length],t);
            deltaTheta = currentTheta - (index > 0 ? thetaForcingFunc[index - 1] : 0);
            if(currentTheta>180) currentTheta-=360;
            ffTheta = new Vector3( -deltaTheta, 0, 0);
            counterCR+=1;
            // if(ffTheta.x>180) ffTheta.x-=360;
        float targetTime = beginTime + elapsedTime + dtPython;
        while (Time.time < targetTime)
        {
            yield return null;
        }
            
            elapsedTime += dtPython;
            index = (index+1 ) % thetaForcingFunc.Length;
        }
        recording = false;
        SaveToFile("theta");
        kill = true;
        move = true;
        Start();

    }
    IEnumerator Training()
    {
        float elapsedTime = 0f;
        int index = 0;
        var beginTime = Time.time;
        while (elapsedTime < T_total)
        {
            float t = elapsedTime % dtPython / dtPython;
            float currentVelocity = Mathf.Lerp(trainingFunc1[index], trainingFunc1[(index + 1) % trainingFunc1.Length], t);

            ffVelocity = new Vector3(0.0f, 0.0f, currentVelocity);
        float targetTime = beginTime + elapsedTime + dtPython;
        while (Time.time < targetTime)
        {
            yield return null;
        }

            elapsedTime += dtPython;

            index = (index + 1) % trainingFunc1.Length;
        }
        recording = false;
        SaveToFile();
        kill = true;
        Start();
    }

    private float u_dot(float u, float theta)
    {
        float theta_1s = theta * (-Mq / M_theta1s);
        float u_dot = X_u * u - g * theta + X_theta1s * theta_1s;
        return u_dot;
    }

    private void AddData(float time, float controlvelocity, float ffvelocity, float controlinput, float fftheta, float controltheta)
    {
        exportData.Add(new Data(time: time, controlvelocity: controlvelocity, ffvelocity: ffvelocity, controlinput: controlinput, fftheta: fftheta, controltheta: controltheta));

    }

    private string ToCsv(string signifier = "v")
    {
        StringBuilder sb;
        if(signifier == "v"){sb = new StringBuilder("Time,CV,FF,Input");}
        else
        sb = new StringBuilder("Time,CT,FF,Input");

        foreach (var entry in exportData)
        {
            if(signifier == "v")
            sb.Append('\n').Append(entry.Time.ToString(CultureInfo.InvariantCulture)).Append(',').
            Append(entry.controlVelocity.ToString(CultureInfo.InvariantCulture)).Append(',').
            Append(entry.ffVelocity.ToString(CultureInfo.InvariantCulture)).Append(',').
            Append(entry.controlInput.ToString(CultureInfo.InvariantCulture))
            ;
            else
            sb.Append('\n').Append(entry.Time.ToString(CultureInfo.InvariantCulture)).Append(',').
            Append(entry.controlTheta.ToString(CultureInfo.InvariantCulture)).Append(',').
            Append(entry.ffTheta.ToString(CultureInfo.InvariantCulture)).Append(',').
            Append(entry.controlInput.ToString(CultureInfo.InvariantCulture))
            ;
        }
        return sb.ToString();
    }
    public void SaveToFile(string signifier ="v")
    {
        // Use the CSV generation from before
        var content = ToCsv(signifier);


        var filePath = "Assets/Scripts/Data/export_";


        using (var writer = new StreamWriter(filePath + id+ "_"+ indicator + "_" + currentFoV.ToString() + ".csv", false))
        {
            writer.Write(content);
        }

        // Or just
        //File.WriteAllText(content);

        Debug.Log($"CSV file written to \"{filePath + id+ "_"+ indicator + "_" + currentFoV.ToString() + ".csv"}\"");


    }
    // Start is called before the first frame update
    void Start()
    {
        recording = false;
        spawnLocation = new(0, 5, -25);
        transform.position = spawnLocation;
        transform.rotation = initialRotation;
        controlVelocity= new  Vector3(0.0f,0.0f,0.0f);
        ffVelocity =  new  Vector3(0.0f,0.0f,0.0f);
        GetData();
        GetTrainingData();
        GetThetaData();
        T_total = T_m + 30;
        currentFoV = baseFoV;
        currentPitch = transform.rotation.x;
        pushValue = Input.GetAxis("Vertical");
        float totalDataPoints = T_total / Time.deltaTime;
        exportData = new List<Data>((int)totalDataPoints);
        ChangeFoV(140);
        if (marker !=null){
            Destroy(marker);
        }
        
        // exportData = new List<Data>((int) 10000);

    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(reset) || Input.GetKeyDown(KeyCode.JoystickButton1)){
            Start();
            StopAllCoroutines();
            kill = false;  
        }
        if (Input.GetKeyDown(FoV20))
        {
            ChangeFoV(20);

        }
        else if (Input.GetKeyDown(FoV30))
        {

            ChangeFoV(30);
        }
        else if (Input.GetKeyDown(FoV60))
        {
            ChangeFoV(60);
        }
        else if (Input.GetKeyDown(FoV90)){
            ChangeFoV(90);
        }
        else if (Input.GetKeyDown(FoV120))
        {
            ChangeFoV(120);
        }
        else if (Input.GetKeyDown(FoV140))
        {
            ChangeFoV(140);
        }

        if (Input.GetKeyDown(startFF) || Input.GetKeyDown(KeyCode.JoystickButton0))
        {
            recording = true;
            beginTIme = Time.time;
            indicator = "actual";
            kill = false;
            StartCoroutine(ChangeVelocity());
            //StartCoroutine(ChangePitch());
        }
        if (Input.GetKeyDown(startTheta) || Input.GetKeyDown(KeyCode.JoystickButton3))
        {
            recording = true;
            beginTIme = Time.time;
            indicator = "theta";
            kill = false;
            SpawnMarker();
            StartCoroutine(ChangeTheta());
            
        }
        if (Input.GetKeyDown(startTraining))
        {
            recording = true;
            beginTIme = Time.time;
            indicator = "training";
            kill = false;
            StartCoroutine(Training());
        }        
        if (Input.GetKey(pitchDown))
        {
            pushValue = 1;

        }
        else if (Input.GetKey(pitchUp))
        {
            pushValue = -1;
        }
        else{
            pushValue = 0 ;
            pushValue = Input.GetAxis("Vertical");
        }
        
        if (!kill)
        {
            angleWanted = pushValue * maxPitchRate / maxVal;
            var thetaDot = angleWanted * M_theta1s;
            finalAngle = thetaDot * Time.deltaTime;
        }
        currentAccel = u_dot(u: controlVelocity.z, theta: currentPitch);
        //Debug.Log($"pitch {currentPitch * Mathf.Rad2Deg} u  {GetComponent<Rigidbody>().velocity.z} accel {currentAccel} dt {Time.deltaTime} velocity {controlVelocity.z}");

        if(move){
            controlVelocity += new Vector3(0.0f, 0.0f, currentAccel * Time.deltaTime);
        }
        
        if (!kill)
        {
            var controlTheta = new Vector3(finalAngle, transform.localEulerAngles.y, transform.localEulerAngles.z);
            var currentEuler = transform.localEulerAngles;
            newEuler = currentEuler + controlTheta;
            
            // newEuler.x= Mathf.Clamp(newEuler.x,-maxPitch,maxPitch);
            if(!move){
                newEuler += ffTheta;
                ffTheta.x = 0;
                counterUp+= 1;
            }
            if(newEuler.x>180) newEuler.x-=360;
            // transform.localEulerAngles = newEuler;
            transform.rotation = Quaternion.Euler(newEuler.x, newEuler.y, newEuler.z);
            var check = transform.eulerAngles.x;
            if(check>180) check-=360;
            if (check<-80){
                transform.rotation = Quaternion.Euler(-80, newEuler.y, newEuler.z);
            }
            else if(check >80){
                transform.rotation = Quaternion.Euler(80, newEuler.y, newEuler.z);
            }
            
            if(marker !=null){
                marker.transform.rotation = Quaternion.Euler(newEuler.x, newEuler.y, newEuler.z);
                var actualTheta = transform.eulerAngles.x;
                if(actualTheta>180){actualTheta -=360;}
                actualTheta = actualTheta * Mathf.Deg2Rad;
                var newZ = Mathf.Cos(actualTheta) * markerDist;
                var newY = Mathf.Tan(actualTheta) * newZ;
                marker.transform.position = new Vector3(0,5 - newY,transform.position.z + newZ);
                Debug.Log($"current orientation {transform.localEulerAngles.x} Actual: {currentTheta}, Cube: {actualTheta}");
            }
            
            //transform.Rotate(Vector3.right, smoothedPitchAngle);
            newPitch = GetPitch();
            currentPitch = newPitch;
            // if(move){
            //     Debug.Log($"value {pushValue} angle wanted {angleWanted} final angle {finalAngle} dt {Time.deltaTime}");
            // }
            
            GetComponent<Rigidbody>().velocity = controlVelocity + ffVelocity;

            
            
        }


        if (recording)
        {
            //Debug.Log($"Time: {Time.time - beginTIme} CV {controlVelocity.z} FF{ffVelocity.z} PV {angleWanted}");
            AddData(Time.time - beginTIme, controlVelocity.z, ffVelocity.z, angleWanted, currentTheta, newEuler.x);
        }


    }
    public void OnDestroy()
    {
    }
}
