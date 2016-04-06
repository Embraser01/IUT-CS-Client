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
    public Slider playerNumberSlider;
    public Slider pieceNumberSlider;
    public Slider gridSizeSlider;

    //Game settings texts
    public Text playerNumberText;
    public Text gridSizeText;
    public Text pieceNumberText;

    //The notification UI
    public Text notificationText;
    
    //Other stuff
    public Text IdGameText;
    public Text ipAddressText;
    public Text endText;
    public Text usernameText;
    public Text joinGameIDText;
    public GameObject casePrefab;

    //For translating the camera
    private float translationStartTime = 0f;
    private Transform originTransform;
    private Transform destinationTransform;
    public Transform startTransform;
    public Transform endTransform;
    public Transform gameTransform;

    public float translationSpeed = 5f;

    #endregion

    #region Network Attributes
    
    private Socket socket;
    private Thread receiveThread;
    private bool canExit = false;
    private Semaphore sem;
    private bool loggedIn = false;
    private bool waitingForTheGame = false;

    #endregion

    #region Game Attributes

    private bool begun = false;
    private bool caseOccupee;
    public int taille = 5;

    private GameObject[] grid;

    public RectTransform panel;

    #endregion

    void Start()
    {

        originTransform = gameObject.transform;
        socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.SendTimeout = 5000;
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
    
    //Smoothly translate the camera
    public void TranslateCamera(Transform destination)
    {
        translationStartTime = Time.time;
        originTransform = gameObject.transform;
        destinationTransform = destination;
    }

    //Connect the client to the server
    public void Connect(Transform destination)
    {
        notificationText.enabled = false;

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
            notificationText.text = e.Message + " " + e.StackTrace;
            notificationText.enabled = true;
        }

        //Start the receive thread
        receiveThread = new Thread(new ThreadStart(ReceiveThread));
        receiveThread.Name = "Receive Thread";
        receiveThread.Start();
    }

    //Log the player in
    public void Login(Transform destination)
    {
        SendData("LOGIN " + usernameText.text);
        sem.WaitOne();
        if (loggedIn)
            TranslateCamera(destination);
    }

    //Create the game
    public void CreatePartie(Transform destination)
    {
        notificationText.enabled = false;
        
        SendData("NEW__ " + gridSizeSlider.value + " " + pieceNumberSlider.value + " " + playerNumberSlider.value);
        
        sem.WaitOne();
        TranslateCamera(destination);
    }

    //Join a game
    public void JoindrePartie(Transform destination)
    {
        notificationText.enabled = false;
        
        SendData("JOIN_ " + joinGameIDText.text);
        
        sem.WaitOne();
        if (waitingForTheGame)
            TranslateCamera(destination);
    }

    //Create the grid
    public void StartGame()
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

        TranslateCamera(gameTransform);
    }

    //Update the grid (deactivate the taken cells)
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

    //Send a command to the server through the network
    public void SendData(string command)
    {
        try
        {
            socket.Send(Encoding.ASCII.GetBytes(command));
        }
        catch (Exception e)
        {
            notificationText.text = e.Message + " " + e.StackTrace;
            notificationText.enabled = true;
        }
    }

    //Thread that execute thing when receiving a command from the server. See the Protocol Specifications for more informations about returned codes
    public void ReceiveThread()
    {
        byte[] buffer;
        string[] data;
        while (!canExit)
        {
            buffer = new byte[2048];
            socket.Receive(buffer);
            data = System.Text.Encoding.Default.GetString(buffer).Split(' ');
            Debug.Log(System.Text.Encoding.Default.GetString(buffer));

            switch (data[0])
            {
                case "100":
                    StartCoroutine(ShowMessage(data[1], 7));
                    break;
                case "101":
                    IdGameText.text = "ID de la partie créée : " + data[1];
                    sem.Release();
                    break;
                case "102":
                    TranslateCamera(endTransform);
                    endText.text = "Partie remportée par le joueur " + data[1] + ".";
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
                    endText.text = "Partie annulée";
                    TranslateCamera(endTransform);
                    break;
                case "107":
                    StopCoroutine("ShowMessage");
                    StartCoroutine(ShowMessage("C'est à votre tour de jouer.", 20));
                    break;
                case "108":
                    UpdateGrid(data);
                    break;
                case "109":
                    loggedIn = true;
                    sem.Release();
                    break;
                case "110":
                    sem.Release();
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
                    sem.Release();
                    break;
                case "203":
                    StopCoroutine("ShowMessage");
                    StartCoroutine(ShowMessage("Case déjà occupée." + data[1], 3));
                    break;
                case "204":
                    StopCoroutine("ShowMessage");
                    StartCoroutine(ShowMessage("La partie est déjà complete.", 7));
                    sem.Release();
                    break;
                case "205":
                    StopCoroutine("ShowMessage");
                    StartCoroutine(ShowMessage("Partie inexistante", 7));
                    sem.Release();
                    break;
                case "206":
                    StopCoroutine("ShowMessage");
                    StartCoroutine(ShowMessage("Vous n'êtes pas autorisé à accéder à cette partie.", 7));
                    break;
                case "207":
                    StopCoroutine("ShowMessage");
                    StartCoroutine(ShowMessage("Nom d'utilisateur déjà utilisé", 7));
                    sem.Release();
                    break;
                case "208":
                    StopCoroutine("ShowMessage");
                    StartCoroutine(ShowMessage("Le nom d'utilisateur contient des caractères incorrects.", 7));
                    sem.Release();
                    break;
                case "209":
                    StopCoroutine("ShowMessage");
                    StartCoroutine(ShowMessage("Ce n'est pas votre tour", 7));
                    break;
                case "210":
                    StopCoroutine("ShowMessage");
                    StartCoroutine(ShowMessage(data[1], 10));
                    break;
                case "300":
                    StopCoroutine("ShowMessage");
                    StartCoroutine(ShowMessage("Erreur serveur : " + data[1], 7));
                    break;

            }

        }
    }

    //Coroutine that show the last information or error
    private IEnumerator ShowMessage(string message, int duration)
    {
        notificationText.text = message;
        notificationText.enabled = true;

        yield return new WaitForSeconds(duration);

        notificationText.enabled = false;
    }

    //When leaving the client, check if the thread is running in order to stap it.
    void OnApplicationQuit()
    {
        if (receiveThread != null)
        {
            receiveThread.Abort();
        }
    }
}
