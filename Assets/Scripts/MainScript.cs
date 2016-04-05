using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class MainScript : MonoBehaviour
{
    //Game settings sliders
    public Slider playerNumberSlider;
    public Slider pieceNumberSlider;
    public Slider gridSizeSlider;

    //Game settings texts
    public Text playerNumberText;
    public Text gridSizeText;
    public Text pieceNumberText;

    //For translating the camera
    private float translationStartTime = 0f;
    private Transform originTransform;
    public Transform destinationTransform;      //Also the point to start (so that's why it is public)

    public float translationSpeed = 5f;

    void Start()
    {
        originTransform = gameObject.transform;
    }

    void Update()
    {
        gameObject.transform.position = new Vector3(
            Mathf.SmoothStep(originTransform.position.x, destinationTransform.position.x, (Time.time - translationStartTime) / translationSpeed),
            Mathf.SmoothStep(originTransform.position.y, destinationTransform.position.y, (Time.time - translationStartTime) / translationSpeed),
            -10f
        );
    }

    public void SetSliders()
    {
        playerNumberSlider.maxValue = gridSizeSlider.value;
        pieceNumberSlider.maxValue = gridSizeSlider.value;

        playerNumberText.text = playerNumberSlider.value.ToString("0");
        gridSizeText.text = gridSizeSlider.value.ToString("0");
        pieceNumberText.text = pieceNumberSlider.value.ToString("0");
    }
    
    public void TranslateCamera(Transform destination)
    {
        translationStartTime = Time.time;
        originTransform = gameObject.transform;
        destinationTransform = destination;
    }

    public void Connect(string ipAddress)
    {

    }
}
