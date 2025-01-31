using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Unity.VisualScripting;
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
    public TextAsset pointFile;
    [SerializeField] GameObject Marker;
    private GameObject marker;

    [SerializeField] GameObject Horizon;
    private GameObject horizon;
    [SerializeField] Sprite MiseryScale;
    [SerializeField] GameObject MiseryScalePrefab;
    private GameObject miseryScale;

    [SerializeField] GameObject EasterEgg;
    private GameObject easterEgg;
    [SerializeField] GameObject MotionPoint;
    private GameObject[] motionPoints;
    private GameObject[] movedMotionPoints;
    [SerializeField] Arrow Arrow;
    private Arrow[] arrow;
    private float markerDist = 5;
    public float scaleDist = 5;

    private float[] forcingFunc;
    private float[] trainingFunc1;
    private float[] trainingFunc2;
    private float[] thetaForcingFunc;
    private float[] xOrg;
    private float[] yOrg;
    private float[] zOrg;
    private float[] xNew;
    private float[] yNew;
    private float[] zNew;

    private float[] time;
    private float[] v;
    private float[] pitch;
    private float dtPython = 0.01f;
    private float T_m = 120.0f;
    private float T_leadIn = 30.0f;
    private float T_total;

    public float pushValue;
    public float pitchSpeed = 1f;
    private float maxPitchRate = 3.0f;
    private float maxVal = 1;
    private float angleWanted;
    private float currentPitch;
    private float finalAngle;
    private float newPitch;
    private Vector3 newEuler;
    private Vector3 newEulerControlOnly;
    private float currentAccel;
    private Vector3 controlVelocity;
    float currentTheta;
    private Vector3 ffVelocity;
    private Vector3 ffTheta;
    private float deltaTheta;
    // private float Mu = 0.0468f;
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
    private KeyCode showMISCScale = KeyCode.H;
    private KeyCode FoV20 = KeyCode.Z;
    private KeyCode FoV30 = KeyCode.X;
    private KeyCode FoV60 = KeyCode.C;
    private KeyCode FoV90 = KeyCode.V;
    private KeyCode FoV120 = KeyCode.B;
    private KeyCode FoV140 = KeyCode.N;
    


    private void SpawnMarker(){
        var markerPos = new Vector3(transform.localPosition.x,transform.localPosition.y,transform.localPosition.z +markerDist);
        var horizonPos = new Vector3(transform.localPosition.x,transform.localPosition.y,transform.localPosition.z + 44);
        if (marker == null){
            marker = Instantiate(Marker, markerPos,transform.rotation);
            horizon = Instantiate(Horizon,horizonPos,transform.rotation);
        }
    }
    private void SpawnScale(){
        var scalePos = new Vector3(transform.localPosition.x,transform.localPosition.y,transform.localPosition.z + scaleDist);
        if (miseryScale == null){
            miseryScale = Instantiate(MiseryScalePrefab,scalePos,transform.rotation);
            SpriteRenderer spriteRenderer = miseryScale.GetComponent<SpriteRenderer>();
            spriteRenderer.sprite = MiseryScale;
            spriteRenderer.transform.position = scalePos;
            
            
        }
    }
    private void SpawnPoints(){
        motionPoints = new GameObject[xOrg.Length];
        for (int i =0; i< xOrg.Length;i++){
            var pointPos = new Vector3(xOrg[i],yOrg[i]+0.5f,zOrg[i]);
            motionPoints[i] = Instantiate(MotionPoint,pointPos,transform.rotation);
        }
    }
    private void SpawnNewPoints(){
        movedMotionPoints = new GameObject[xNew.Length];
        for (int i =0; i< xNew.Length;i++){
            var pointPos = new Vector3(xNew[i],yNew[i]+0.5f,zNew[i]);
            movedMotionPoints[i] = Instantiate(MotionPoint,pointPos,transform.rotation);
            movedMotionPoints[i].GetComponent<Renderer>().material.color = Color.yellow;
        }  
    }
    private void SpawnArrows(){
        arrow = new Arrow[xNew.Length];
        Quaternion xRotationFix = Quaternion.Euler(90,0,0);

        float zOffset = 0.76f;
        Vector3 offsetVector = new Vector3(0,0,zOffset);
        for(int i =0; i<xNew.Length;i++){
            var Pos = new Vector3(xOrg[i],yOrg[i]+0.5f,zOrg[i]);
            var target = new Vector3(xNew[i], yNew[i] + 0.5f, zNew[i]);
            Vector3 direction = target - Pos;
            var yScaling = direction.magnitude / 3.451f;

            
            // var xRotation = Mathf.Atan((zNew[i] - zOrg[i]) / (yNew[i] - yOrg[i])) * Mathf.Rad2Deg;
            // //var yRotation = Mathf.Atan((xNew[i] - xOrg[i]) / (zNew[i] - zOrg[i])) * Mathf.Rad2Deg;
            // var yRotation = Mathf.Atan((zNew[i] - zOrg[i]) / (xNew[i] - xOrg[i])) * Mathf.Rad2Deg;
            arrow[i] = Instantiate(Arrow,Pos,transform.rotation);
            var arrowPoint = arrow[i].transform.rotation;
            if(direction!= Vector3.zero){
                arrowPoint = Quaternion.LookRotation(direction);
            }
            
            
        
            if(yScaling == 0){
                yScaling = 0.02f;
            }
            if(yScaling>0.2){
                arrow[i].transform.localScale = new Vector3(0.2f,yScaling,0.2f);
            }
            else{
                arrow[i].transform.localScale = new Vector3(yScaling,yScaling,yScaling);
            }

            
            arrow[i].transform.rotation = arrowPoint;
            Vector3 tiltedOffset = arrow[i].transform.rotation * offsetVector * yScaling;
            arrow[i].transform.position += tiltedOffset;
            arrow[i].transform.rotation *= xRotationFix;

            
            // arrow[i].transform.position += new Vector3(0,zOffset,0);

        }
        
    }
    private Vector3 Movement(Vector3 pointOrigin, Vector3 dx, float theta)
    {
        // Create the rotation matrix (Ry for rotation around the Y-axis)
        float cosTheta = Mathf.Cos(theta);
        float sinTheta = Mathf.Sin(theta);

        // Rotation matrix for Y-axis rotation (3x3)
        Matrix4x4 Ry = new Matrix4x4();
        Ry.SetRow(0, new Vector4(1, 0, 0, 0));
        // Ry.SetRow(1, new Vector4(-sinTheta, 0, cosTheta, 0));
        Ry.SetRow(1, new Vector4(0,cosTheta,-sinTheta,0));
        // Ry.SetRow(2, new Vector4(cosTheta, 0, sinTheta, 0));
        Ry.SetRow(2, new Vector4(0, sinTheta, cosTheta, 0));
        Ry.SetRow(3, new Vector4(0, 0, 0, 1)); // Homogeneous row
        // Multiply the rotation matrix by the pointOrigin
        Vector3 newOrigin = Ry.MultiplyPoint3x4(pointOrigin) - dx;
        
        // Debug.Log($"Calculations theta {theta} dx {dx} act {Ry.MultiplyPoint3x4(pointOrigin)} ");
        // Return the new origin and its components (x, y, z)
        return newOrigin;
    }
    private void CalcMovement(float u, float theta, float dt){
        Vector3 buffer = new(0,0,0);
        for (int i =0; i<xNew.Length;i++){
            buffer.x = xOrg[i];
            buffer.y = yOrg[i];
            buffer.z = zOrg[i];
            Vector3 dx = new Vector3(0,0,u*dt);
            Vector3 outVec = Movement(buffer,dx,theta);
            xNew[i] = outVec.x;
            yNew[i] = outVec.y;
            zNew[i] = outVec.z;
            
        }
        
    }
    private void CalcMovement(float u, float thetaBefore, float thetaNow, float dt){
        Vector3 buffer = new(0,0,0);
        for (int i =0; i<xNew.Length;i++){
            buffer.x = xOrg[i];
            buffer.y = yOrg[i];
            buffer.z = zOrg[i];
            Vector3 dx = new Vector3(0,0,u*dt);
            var theta = thetaNow - thetaBefore;
            Vector3 outVec = Movement(buffer,dx,theta);
            xNew[i] = outVec.x;
            yNew[i] = outVec.y;
            zNew[i] = outVec.z;
            
        }
        
    }
    private IEnumerator ScreenCap(){
        // double[] files = {0.5, 1.0, 1.5, 2.0, 2.5, 3.0, 3.5, 4.0};
        string[] files = {"coordsNew0.5","coordsNew1","coordsNew1.5","coordsNew2","coordsNew2.5","coordsNew3","coordsNew3.5","coordsNew4"};
        foreach (string num in files){
            Debug.Log(num);
            string picName = "Assets/Scripts/Images/Flow" + num.ToString() + ".png";
            transform.position = new Vector3(0,5,0);
            GetPointData();
            //SpawnPoints();
            GetNewPointData(num);
            //SpawnNewPoints();
            SpawnArrows();
            ScreenCapture.CaptureScreenshot(picName);
            yield return new WaitForSeconds(5);
            for (int i =0; i<arrow.Count<Arrow>(); i++){
                Destroy(arrow[i].gameObject);
            }

        }
        
    }
    private void TakeScreenshot(float v, float theta, float time){
        string picFolder =  "Assets/Scripts/Images/";
        string picName = "ActualPitchFlow_t" + time.ToString("F2") + "_v" +  v.ToString("F2") + "_p" + theta.ToString("F2")+".png";
        ScreenCapture.CaptureScreenshot(picFolder + picName); 

    }

    private IEnumerator FlowAnimation(){
        GetBehaviourData("actualMove",true);
        Debug.Log("Part 1 done");
        GetPointData();
        transform.position = new Vector3(0,5,0);
        float[] timeStamps = Enumerable.Range(0, (150 - 5) / 5)
                                       .Select(i => 5 + i * 5)
                                       .Select(i => (float)i)
                                       .ToArray();
        Debug.Log(timeStamps[0]);
        // float[] timeStamps = {20f, 40f, 60f, 80f, 100,120,140};
        //float[] timeStamps = {0.5f, 1.0f, 1.5f, 2.5f, 3.5f, 4.0f};
        int counter = 0;
        // CalcMovement(v[1],pitch[1],time[1]-time[0]);
        //SpawnPoints();
        for (int i=1; i< time.Length;i++){
            var dt = time[i] - time[i-1];
            v[i] = 0;
            // pitch[i-1] = 0;
            // pitch[i] = 0;
            
            // CalcMovement(v[i],pitch[i],dt);
            CalcMovement(v[i],pitch[i-1],pitch[i],dt);
            SpawnArrows();
            if((time[i]>= timeStamps[counter] - 0.25 ) & (time[i] < timeStamps[counter]+0.25)){
            
                TakeScreenshot(v[i],pitch[i],time[i]);
                Debug.Log($" time {time[i]} v {v[i]} theta {pitch[i] * Mathf.Rad2Deg} dt {dt} counter {counter}");
            }
            if(time[i] > timeStamps[counter]+0.25){
                counter+=1;
            }
            //Debug.Log($" time {time[i]} v {v[i]} theta {pitch[i] * Mathf.Rad2Deg} dt {dt} counter {counter}");
            if(counter > timeStamps.Length-1){
                counter = timeStamps.Length -1;
            }
            yield return new WaitForSeconds(dt);
            for (int j =0; j<arrow.Count<Arrow>(); j++){
                Destroy(arrow[j].gameObject);
            }
            // for (int k =0; k<movedMotionPoints.Count<GameObject>();k++){
            //     Destroy(movedMotionPoints[k]);
            // }                           
        }
        yield return null;
    }

    private IEnumerator MoveAnimation(){
        GetBehaviourData("actualMoveFPS",true);
        float timestep = time[1]-time[0];
        for (int i =1; i<time.Length;i++){
            GetComponent<Rigidbody>().velocity = new Vector3(0f,0f,v[i]);
            var rotateVector = new Vector3(pitch[i] * Mathf.Rad2Deg, transform.localEulerAngles.y, transform.localEulerAngles.z);
            transform.rotation = Quaternion.Euler(rotateVector.x,rotateVector.y,rotateVector.z);
            timestep = time[i] - time[i-1];
            yield return new WaitForSeconds(timestep);
        }
        kill = true;
        Start();

        
    }
    IEnumerator SpawnEasterEgg(){
        int randVal = UnityEngine.Random.Range(0,30);
        if (randVal == 0){
            Debug.LogWarning("BOO!");
            var scaleImg = new Vector3(transform.localPosition.x,transform.localPosition.y,transform.localPosition.z + scaleDist-2);
            if (easterEgg == null){
                easterEgg = Instantiate(EasterEgg,scaleImg,transform.rotation);
            }
            yield return new WaitForSeconds(0.3f);
            Destroy(easterEgg);

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
    private void GetPointData(){
        string[] data = pointFile.text.Split(new string[] {",", "\n"},StringSplitOptions.None);
        int tableSize = data.Length / 4 -1;
        xOrg = new float[tableSize];
        yOrg = new float[tableSize];
        zOrg = new float[tableSize];
        xNew = new float[tableSize];
        yNew = new float[tableSize];
        zNew = new float[tableSize];
        for (int i=0;i<tableSize; i++){
            var value = float.Parse(data[4 * (i+1) + 1],CultureInfo.InvariantCulture);
            xOrg[i] = value;
            var value2 = float.Parse(data[4 * (i+1) + 2],CultureInfo.InvariantCulture);
            yOrg[i] = value2;
            var value3 = float.Parse(data[4 * (i+1) + 3],CultureInfo.InvariantCulture);
            zOrg[i] = value3;
        }

    }
    private void GetNewPointData(string fileName){
        TextAsset newPointFile = Resources.Load<TextAsset>(fileName);
        string[] data = newPointFile.text.Split(new string[] {",", "\n"},StringSplitOptions.None);
        int tableSize = data.Length / 4 -1;
        xNew = new float[tableSize];
        yNew = new float[tableSize];
        zNew = new float[tableSize];
        for (int i=0;i<tableSize; i++){
            var value = float.Parse(data[4 * (i+1) + 1],CultureInfo.InvariantCulture);
            xNew[i] = value;
            var value2 = float.Parse(data[4 * (i+1) + 2],CultureInfo.InvariantCulture);
            yNew[i] = value2;
            var value3 = float.Parse(data[4 * (i+1) + 3],CultureInfo.InvariantCulture);
            zNew[i] = value3;
        }
    }
    private void GetBehaviourData(string fileName, bool actual){
        TextAsset behaviourFile = Resources.Load<TextAsset>(fileName);
        string[] data = behaviourFile.text.Split(new string[] {",", "\n"},StringSplitOptions.None);
        if(actual){
            int tableSize = data.Length / 6 -1;
            time = new float[tableSize];
            v = new float[tableSize];
            pitch = new float[tableSize];
            for (int i=0;i<tableSize; i++){
                var value = float.Parse(data[6 * (i+1) + 1],CultureInfo.InvariantCulture);
                time[i] = value;
                var value2 = float.Parse(data[6 * (i+1) + 2],CultureInfo.InvariantCulture);
                v[i] = value2;
                var value3 = float.Parse(data[6 * (i+1) + 4],CultureInfo.InvariantCulture);
                pitch[i] = value3;
            }
        }
        else{
            int tableSize = data.Length / 8 -1;
            time = new float[tableSize];
            v = new float[tableSize];
            pitch = new float[tableSize];
            for (int i=0;i<tableSize; i++){
                var value = float.Parse(data[8 * (i+1) + 1],CultureInfo.InvariantCulture);
                time[i] = value;
                var value2 = float.Parse(data[8 * (i+1) + 5],CultureInfo.InvariantCulture);
                v[i] = value2;
                var value3 = float.Parse(data[8 * (i+1) + 3],CultureInfo.InvariantCulture);
                pitch[i] = value3;
            }           
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
            yield return new WaitForSeconds(dtPython);
        }

        elapsedTime += dtPython;

        index = (index + 1) % forcingFunc.Length;
        Debug.Log($"elapsed time {elapsedTime} actual {Time.time - beginTIme} diff {Time.time - beginTIme -elapsedTime}");
        }
        recording = false;
        SaveToFile();
        kill = true;
        Start();
        StartCoroutine(SpawnEasterEgg());
        SpawnScale();
    }
    IEnumerator ChangeTheta(){
        float elapsedTime = 0f;
        int index = 0;
        var beginTime = Time.time;
        var timePassed= Time.time;
        float targetTime;
        while(elapsedTime < T_total){
            //float t = elapsedTime % dtPython / dtPython;
            float t = (Time.time - timePassed) /dtPython;
            //currentTheta = Mathf.Lerp(thetaForcingFunc[index], thetaForcingFunc[(index+1) % thetaForcingFunc.Length],t);
            currentTheta = thetaForcingFunc[index];
            deltaTheta = currentTheta - (index > 0 ? thetaForcingFunc[index - 1] : 0);
            if(currentTheta>180) currentTheta-=360;
            ffTheta = new Vector3( -deltaTheta, 0, 0);
            counterCR+=1;
            // if(ffTheta.x>180) ffTheta.x-=360;
        targetTime = beginTime + elapsedTime + dtPython;
        while (Time.time < targetTime)
        {
            yield return new WaitForSeconds(dtPython);
        }
        
            
        elapsedTime += dtPython;
        index = (index+1 ) % thetaForcingFunc.Length;
        timePassed = Time.time;
        }
        recording = false;
        SaveToFile("theta");
        kill = true;
        move = true;
        Start();
        StartCoroutine(SpawnEasterEgg());
        SpawnScale();

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
            yield return new WaitForSeconds(dtPython);
        }

            elapsedTime += dtPython;

            index = (index + 1) % trainingFunc1.Length;
        }
        recording = false;
        SaveToFile();
        kill = true;
        Start();
        StartCoroutine(SpawnEasterEgg());
        SpawnScale();
    }
    IEnumerator Dynamics(){
        while(true){
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
            finalAngle = thetaDot * dtPython;
        }
        currentAccel = u_dot(u: controlVelocity.z, theta: currentPitch);
        //Debug.Log($"pitch {currentPitch * Mathf.Rad2Deg} u  {GetComponent<Rigidbody>().velocity.z} accel {currentAccel} dt {Time.deltaTime} velocity {controlVelocity.z}");

        if(move){
            controlVelocity += new Vector3(0.0f, 0.0f, currentAccel * dtPython);
        }
        
        if (!kill)
        {
            var controlTheta = new Vector3(finalAngle, transform.localEulerAngles.y, transform.localEulerAngles.z);
            var currentEuler = transform.localEulerAngles;
            newEuler = currentEuler + controlTheta;
            newEulerControlOnly = currentEuler + controlTheta;
            // newEuler.x= Mathf.Clamp(newEuler.x,-maxPitch,maxPitch);
            if(!move){
                
                newEuler += ffTheta;
                
                counterUp+= 1;
            }
            //if(newEuler.x>180) newEuler.x-=360;
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

            // if(move){
            //     Debug.Log($"value {pushValue} angle wanted {angleWanted} final angle {finalAngle} dt {Time.deltaTime}");
            // }
            
            GetComponent<Rigidbody>().velocity = controlVelocity + ffVelocity;

            
            
        }
        if (recording)
        {
            
            Debug.Log($"Time: {Time.time - beginTIme} CV {controlVelocity.z} FF{ffVelocity.z} PV {angleWanted} CT {finalAngle} CP {currentPitch*Mathf.Rad2Deg} FFT {ffTheta.x} ");
            AddData(Time.time - beginTIme, controlVelocity.z, ffVelocity.z, angleWanted, ffTheta.x, finalAngle,currentPitch*Mathf.Rad2Deg);
        }
        newPitch = GetPitch();
        currentPitch = newPitch;
        ffTheta.x = 0;
        yield return new WaitForSeconds(dtPython);
        }
    }

    private float u_dot(float u, float theta)
    {
        float theta_1s = theta * (-Mq / M_theta1s);
        float u_dot = X_u * u - g * theta + X_theta1s * theta_1s;
        return u_dot;
    }

    private void AddData(float time, float controlvelocity, float ffvelocity, float controlinput, float fftheta, float controltheta,float currentPitch)
    {
        exportData.Add(new Data(time: time, 
                                controlvelocity: controlvelocity, 
                                ffvelocity: ffvelocity, 
                                controlinput: controlinput, 
                                fftheta: fftheta, 
                                controltheta: controltheta,
                                currentpitch:currentPitch));

    }

    private string ToCsv(string signifier = "v")
    {
        StringBuilder sb;
        if(signifier == "v"){sb = new StringBuilder("Time,CV,FF,Input");}
        else
        sb = new StringBuilder("Time,CT,FF,Input,Pitch");

        foreach (var entry in exportData)
        {
            if(signifier == "v"){
            sb.Append('\n').Append(entry.Time.ToString(CultureInfo.InvariantCulture)).Append(',').
            Append(entry.controlVelocity.ToString(CultureInfo.InvariantCulture)).Append(',').
            Append(entry.ffVelocity.ToString(CultureInfo.InvariantCulture)).Append(',').
            Append(entry.controlInput.ToString(CultureInfo.InvariantCulture))
            ;}
            else{
            sb.Append('\n').Append(entry.Time.ToString(CultureInfo.InvariantCulture)).Append(',').
            Append(entry.controlTheta.ToString(CultureInfo.InvariantCulture)).Append(',').
            Append(entry.ffTheta.ToString(CultureInfo.InvariantCulture)).Append(',').
            Append(entry.controlInput.ToString(CultureInfo.InvariantCulture)).Append(',').
            Append(entry.currentPitch.ToString(CultureInfo.InvariantCulture))
            ;}
        }
        return sb.ToString();
    }
    public void SaveToFile(string signifier ="v")
    {
        // Use the CSV generation from before
        var content = ToCsv(signifier);

        
        var filePath = Path.Combine(Application.streamingAssetsPath, "Data/export_");
        // var filePath = "Assets/Scripts/Data/export_";
        id += Time.time.ToString();
        


        using (var writer = new StreamWriter(filePath + id+ "_"+ indicator + "_" + currentFoV.ToString() + ".csv", false))
        {
            writer.Write(content);
        }


        Debug.Log($"CSV file written to \"{filePath + id+ "_"+ indicator + "_" + currentFoV.ToString() + ".csv"}\"");


    }
    
    // Start is called before the first frame update
    void Start()
    {
        recording = false;
        move = true;
        spawnLocation = new(0, 5, -25);
        transform.position = spawnLocation;
        transform.rotation = initialRotation;
        controlVelocity= new  Vector3(0.0f,0.0f,0.0f);
        ffVelocity =  new  Vector3(0.0f,0.0f,0.0f);
        GetData();
        GetTrainingData();
        GetThetaData();
        GetPointData();
        T_total = T_m + T_leadIn;
        currentFoV = baseFoV;
        currentPitch = transform.rotation.x;
        pushValue = Input.GetAxis("Vertical");
        float totalDataPoints = T_total / Time.deltaTime;
        exportData = new List<Data>((int)totalDataPoints);
        ChangeFoV(140);
        if (marker !=null){
            Destroy(marker);
        }
        if (horizon !=null){
            Destroy(horizon);
        }
        StartCoroutine(Dynamics());
        
        // exportData = new List<Data>((int) 10000);

    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.A)){
            // StartCoroutine(ScreenCap());
            // StartCoroutine(FlowAnimation());
            StartCoroutine(MoveAnimation());
        }
        if (Input.GetKeyDown(showMISCScale)){
            SpawnScale();
            //StartCoroutine(SpawnEasterEgg());
        }
        if (Input.GetKeyDown(reset) ){
            for (int i =0; i<arrow.Count<Arrow>(); i++){
                Destroy(arrow[i].gameObject);
            }  
            StopAllCoroutines();
            Start();
            if(miseryScale != null){
                Destroy(miseryScale);
            }

                  
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

        if (Input.GetKeyDown(startFF))
        {
            recording = true;
            beginTIme = Time.time;
            kill = false;
            if(move){
                indicator = "actual";
                StartCoroutine(ChangeVelocity());
            }
            else{
                StartCoroutine(ChangeTheta());
            }
            if(miseryScale != null){
                Destroy(miseryScale);
            }
            
            //StartCoroutine(ChangePitch());
        }
        if (Input.GetKeyDown(startTheta) )
        {
            beginTIme = Time.time;
            indicator = "theta";
            kill = false;
            SpawnMarker();
            move = false;
            if(miseryScale != null){
                Destroy(miseryScale);
            }            
            //
            
        }
        if (Input.GetKeyDown(startTraining))
        {
            recording = true;
            beginTIme = Time.time;
            indicator = "training";
            kill = false;
            if(miseryScale != null){
                Destroy(miseryScale);
            }            
            StartCoroutine(Training());
        }        

    }
    public void OnDestroy()
    {
    }
}
