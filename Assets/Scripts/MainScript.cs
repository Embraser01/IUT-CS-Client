using System;
using UnityEngine;
using UnityEngine.UI;
using System.Net;
using System.Collections;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;


public class MainScript : MonoBehaviour
{

    #region UI Attributes

    //Game settings sliders
    [HideInInspector] public Slider playerNumberSlider;
    [HideInInspector] public Slider pieceNumberSlider;
    [HideInInspector] public Slider gridSizeSlider;

    //Game settings texts
    [HideInInspector] public Text playerNumberText;
    [HideInInspector] public Text gridSizeText;
    [HideInInspector] public Text pieceNumberText;

    [HideInInspector] public Text errorText;
    
    public Text ipAddressText;
    public GameObject casePrefab;

    //For translating the camera
    private float translationStartTime = 0f;
    private Transform originTransform;
    private Transform destinationTransform;      //Also the point to start (so that's why it is public)
    public Transform startTransform;
    public Transform endTransform;

    public float translationSpeed = 5f;

    #endregion

    #region Network Attributes

    private TcpClient client;
    private Socket socket;
    private Thread sendThread, receiveThread;
    private bool canExit = false;
    private Semaphore sem;

    #endregion

    #region Game Attributes

    private bool begun = false;
    private bool caseOccupee;
    private int taille;

    private GameObject[] grid;

    public RectTransform panel;

    #endregion

    void Start()
    {
        originTransform = gameObject.transform;
        socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        sem = new Semaphore(0, 1);
        destinationTransform = startTransform;
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

    public void Connect(Transform destination)
    {
        errorText.enabled = false;

        try
        {
            IPHostEntry ipHostInfo = Dns.Resolve(Dns.GetHostName());
            IPAddress ipAddress = IPAddress.Parse(this.ipAddressText.text);
            IPEndPoint remoteEP = new IPEndPoint(ipAddress, 3490);

            socket.Connect(remoteEP);

            TranslateCamera(destination);
        }
        catch (Exception e)
        {
            //errorText.text = "Impossible de se connecter au serveur. Veillez à ce que l'adresse IP soit correcte.";
            errorText.text = e.Message + " " + e.StackTrace;
            errorText.enabled = true;
        }

        /*sendThread = new Thread(new ThreadStart(SendThread));
        sendThread.Name = "Send Thread";
        sendThread.Start();*/

        receiveThread = new Thread(new ThreadStart(ReceiveThread));
        receiveThread.Name = "Receive Thread";
        receiveThread.Start();
    }

    public void CreatePartie()
    {
        errorText.enabled = false;
        SendData("NEW__ " + gridSizeSlider.value + " " + pieceNumberSlider.value + " " + playerNumberSlider.value);
        sem.WaitOne();
    }

    public void JoindrePartie(Text field)
    {
        errorText.enabled = false;
        SendData("JOIN_ " + field.text);
    }

    private void StartGame()
    {
        GridLayoutGroup gridLayoutGroup = panel.GetComponent<GridLayoutGroup>();
        gridLayoutGroup.cellSize = new Vector2(300/taille, 300/taille);
        gridLayoutGroup.constraintCount = taille;
        grid = new GameObject[taille*taille];
        GameObject tmpCase;

        for (int i = 0; i < taille; i++)
        {
            for (int j = 0; j < taille; j++)
            {
                tmpCase = Instantiate(casePrefab);
                tmpCase.transform.parent = panel;
                tmpCase.GetComponentInChildren<Button>().onClick.AddListener(() => SendData("PLAY_ " + i + " " + j));
                grid[i*taille + j] = tmpCase;
            }
        }
    }

    private void UpdateGrid(string[] data)
    {
        for (int i = 0; i < taille; i++)
        {
            for (int j = 0; j < taille; j++)
            {
                grid[i * taille + j].GetComponent<Button>().interactable = (data[i * taille + j + 2] == "0");
                grid[i * taille + j].GetComponentInChildren<Text>().text = data[i * taille + j + 2];
            }
        }
    }

    public void SendData(string command)
    {
        try
        {
            socket.Send(Encoding.ASCII.GetBytes(command));
        }
        catch (Exception e)
        {
            errorText.text = e.Message + " " + e.StackTrace;
            errorText.enabled = true;
        }
    }

    public void ReceiveThread()
    {
        byte[] buffer = new byte[2048];
        string[] data;
        while (!canExit)
        {
            socket.Receive(buffer);
            data = buffer.ToString().Split(' ');

            switch (data[0])
            {
                case "100":
                    StartCoroutine(ShowMessage(data[1], 7));
                    break;
                case "101":
                    StopCoroutine("ShowMessage");
                    StartCoroutine(ShowMessage("ID partie : " + data[1] + ".", 7));
                    break;
                case "102":
                    StopCoroutine("ShowMessage");
                    StartCoroutine(ShowMessage("Partie remportée par le joueur " + data[1] + ".", 20));
                    break;
                case "103":
                    StopCoroutine("ShowMessage");
                    StartCoroutine(ShowMessage("Score : " + data[1], 3));
                    break;
                case "104":
                    caseOccupee = data[1] == "true";
                    sem.Release();
                    break;
                case "105":
                    if (!begun && data[1] == "true")
                    {
                            begun = true;
                            StartGame();
                    }
                    sem.Release();
                    break;
                case "106":
                    StopCoroutine("ShowMessage");
                    StartCoroutine(ShowMessage("Partie annulée.", 20));
                    //TranslateCamera();
                    break;
                case "107":
                    StopCoroutine("ShowMessage");
                    StartCoroutine(ShowMessage("C'est à votre tour de jouer.", 20));
                    break;
                case "108":
                    UpdateGrid(data);
                    break;
                case "200":
                    StopCoroutine("ShowMessage");
                    StartCoroutine(ShowMessage("Erreur serveur : " + data[1] + ".", 7));
                    break;
                case "201":
                    StopCoroutine("ShowMessage");
                    StartCoroutine(ShowMessage("Problème dans les paramètres de la partie.", 7));
                    break;
                case "202":
                    StopCoroutine("ShowMessage");
                    StartCoroutine(ShowMessage("Case hors de la grille.", 3));
                    break;
                case "203":
                    StopCoroutine("ShowMessage");
                    StartCoroutine(ShowMessage("Case déjà occupée." + data[1], 3));
                    break;
                case "204":
                    StopCoroutine("ShowMessage");
                    StartCoroutine(ShowMessage("La partie est déjà complete.", 7));
                    break;
                case "205":
                    StopCoroutine("ShowMessage");
                    StartCoroutine(ShowMessage("Partie inexistante", 7));
                    break;
                case "206":
                    StopCoroutine("ShowMessage");
                    StartCoroutine(ShowMessage("Vous n'êtes pas autorisé à accéder à cette partie.", 7));
                    break;
                case "207":
                    StopCoroutine("ShowMessage");
                    StartCoroutine(ShowMessage("Vous n'êtes pas autorisé à accéder à cette partie.", 7));
                    break;
                case "208":
                    StopCoroutine("ShowMessage");
                    StartCoroutine(ShowMessage("Vous n'êtes pas autorisé à accéder à cette partie.", 7));
                    break;
                case "209":
                    StopCoroutine("ShowMessage");
                    StartCoroutine(ShowMessage("Vous n'êtes pas autorisé à accéder à cette partie.", 7));
                    break;
                case "210":
                    StopCoroutine("ShowMessage");
                    StartCoroutine(ShowMessage("Vous n'êtes pas autorisé à accéder à cette partie.", 7));
                    break;
                case "300":
                    StopCoroutine("ShowMessage");
                    StartCoroutine(ShowMessage("Erreur serveur : " + data[1], 7));
                    break;

            }

        }
    }

    private IEnumerator ShowMessage(string message, int duration)
    {
        errorText.text = message;
        errorText.enabled = true;

        yield return new WaitForSeconds(duration);

        errorText.enabled = false;
    }
}
