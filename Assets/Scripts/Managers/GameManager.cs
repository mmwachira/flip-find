using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using DG.Tweening;// Make sure to include the DOTween namespace

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public Image GameBackground => gameBackground;
    public Sprite[] CurrentCardBatch => currentCardBatch;
    public bool IsCheckingForMatch => isCheckingForMatch; // Public property to access isCheckingForMatch

    public bool isGameOver;
    public bool _newGame = false;
    public GameObject cardPrefab;
    public Transform gridTransform;
    public Transform fullImageTransform; // Reference to the UI element to show full image
    public Image fullImageDisplay; // Image component to display the full image
    public AudioClip backgroundMusic; // Reference to the background music AudioClip
    public AudioClip flipSound; // Reference to the card flip sound AudioClip
    public CardBatch[] cardBatches;
    public Sprite[] batchBackground;

    [SerializeField] private Animator heartAnimator;

    [SerializeField] private Image gameBackground;
    [SerializeField] private Image heart;

    [SerializeField] private GameObject mainMenu;
    [SerializeField] private GameObject feedbackBackground;
    [SerializeField] private GameObject gameOverbackground;

    [SerializeField] private TMP_Text totalScore;
    [SerializeField] private TMP_Text moveCounter;
    [SerializeField] private TMP_Text _feedBackMessage;
    [SerializeField] private TMP_Text lastLevel;
    [SerializeField] private TMP_Text finalTime;

    private AudioSource audioSource; // AudioSource component to play the background music and sounds
    private Card firstFlippedCard;
    private Card secondFlippedCard;
    private int currentMoves = 0;
    private int remainingMoves;
    private const int maxMoves = 36;
    private GameData gameData;
    private GoogleAdsInitializer adMob;
    private Sprite[] currentCardBatch;
    private bool isCheckingForMatch = false; // Flag to prevent additional flips while checking for match

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
        audioSource = gameObject.AddComponent<AudioSource>();

        AudioMixerGroup[] audioMixerGroups = Resources.Load<AudioMixer>("VolumeMixer").FindMatchingGroups("Master");

        if (audioMixerGroups.Length > 0)
        {
            audioSource.outputAudioMixerGroup = audioMixerGroups[0];
        }
    }

    void Start()
    {
        gameData = new GameData();
        adMob = new GoogleAdsInitializer();
        PlayBackgroundMusic();
        StartCoroutine(InitializeGame());
        gameBackground.sprite = batchBackground[0];

    }

    private IEnumerator InitializeGame()
    {
        yield return new WaitUntil(() => CollectablesManager.instance != null && UIManager.Instance != null);

        gameData.LoadGame();
        LoadCardBatch();
        //LoadBatchBackground();
        GenerateCards();
        remainingMoves = maxMoves;
        heartAnimator.SetBool("Reset", false);
        moveCounter.text = remainingMoves.ToString();

        if (GoogleAdsInitializer.Instance.IsOnline()) // Ensure the ads initializer has a singleton or public instance
        {
            adMob.RequestBanner();
        }
        else
        {
            Debug.LogWarning("No internet connection. Skipping banner ad request.");
        }


    }

    public void Quit()
    {
        Application.Quit();
    }

    public void GenerateCards()
    {

        List<Sprite> images = new List<Sprite>(currentCardBatch);
        images.AddRange(currentCardBatch); // Duplicate images for pairs
        images = ShuffleList(images);

        foreach (Sprite image in images)
        {
            GameObject cardObject = Instantiate(cardPrefab, gridTransform);
            Card card = cardObject.GetComponent<Card>();
            card.SetCardImage(image);
            Sprite currentCardBack = CollectablesManager.instance.GetCurrentCardBack();


            card.SetCardBack(currentCardBack);
            CardAnim cardAnim = cardObject.GetComponent<CardAnim>();
            cardAnim.Init(card); // Initialize CardAnim with the Card reference
        }
    }

    public void LoadCardBatch()
    {
        int level = CollectablesManager.instance.CurrentLevel;

        if (_newGame == true)
        {
            level = 0;
        }
        if (level < cardBatches.Length)
        {
            currentCardBatch = cardBatches[level].cardFaces;
        }
        else
        {
            int randomIndex = Random.Range(0, cardBatches.Length);
            currentCardBatch = cardBatches[randomIndex].cardFaces;
        }
    }

    public void LoadBatchBackground()
    {
        int level = CollectablesManager.instance.CurrentLevel;

        if (_newGame == true)
        {
            level = 0;
        }
        if (level < batchBackground.Length)
        {
            gameBackground.sprite = batchBackground[level];
        }
        else
        {
            int randomIndex = Random.Range(0, batchBackground.Length);
            gameBackground.sprite = batchBackground[randomIndex];
        }

    }

    List<Sprite> ShuffleList(List<Sprite> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            Sprite temp = list[i];
            int randomIndex = Random.Range(0, list.Count);
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
        return list;
    }

    [System.Obsolete]
    public void OnCardFlipped(Card card)
    {
        if (isCheckingForMatch)
            return; // Prevent flipping if a match check is in progress

        // Play flip sound
        PlayFlipSound();

        currentMoves++;
        remainingMoves--;
        moveCounter.text = remainingMoves.ToString();

        if (remainingMoves <= 24)
        {
            heartAnimator.SetBool("Reset", false);
            heartAnimator.SetBool("isLow", true);
            moveCounter.color = Color.yellow;
        }

        if (remainingMoves <= 12)
        {
            heartAnimator.SetBool("Reset", false);
            heartAnimator.SetBool("isVeryLow", true);
            moveCounter.color = Color.red;
        }


        if (currentMoves > maxMoves)
        {
            GameOver();
        }

        feedbackBackground.SetActive(false);

        if (firstFlippedCard == null)
        {
            firstFlippedCard = card;
        }
        else if (secondFlippedCard == null)
        {
            secondFlippedCard = card;
            isCheckingForMatch = true; // Set flag to indicate match check is in progress
            StartCoroutine(CheckForMatch());
        }
    }

    private IEnumerator CheckForMatch()
    {
        yield return new WaitForSeconds(1); // Wait for a second to let the player see the flipped cards

        if (firstFlippedCard.cardFront == secondFlippedCard.cardFront)
        {
            // Match found
            fullImageDisplay.sprite = firstFlippedCard.cardFront;
            firstFlippedCard.SetMatched();
            secondFlippedCard.SetMatched();

            // Show score message
            CollectablesManager.instance.ShowScoreMessage();

            // Update score
            UIManager.Instance.AddScore(10); // Add points for a correct match

            ShowFeedbackMessage();

            CheckLevelCompletion();
        }
        else
        {
            // No match, flip cards back
            firstFlippedCard.GetComponent<CardAnim>().FlipBack();
            secondFlippedCard.GetComponent<CardAnim>().FlipBack();
        }

        // Reset flipped cards
        firstFlippedCard = null;
        secondFlippedCard = null;
        isCheckingForMatch = false; // Clear flag after match check is complete
    }

    private void ShowFeedbackMessage()
    {
        List<string> messages = new List<string>
        {
            "Great job!",
            "You're getting good at this!",
            "Fantastic! Keep it up!",
            "Amazing skills!",
            "Incredible! You're unstoppable!",
            "Wow! Keep it up!",
            "That was great!",
            "You're a natural!",
            "You nailed it!",
        };

        int index = Random.Range(0, messages.Count);

        if (UIManager.Instance.CardFaceScore == 30)
        {
            feedbackBackground.SetActive(true);

            _feedBackMessage.text = messages[index];
        }

        _feedBackMessage.alpha = 0; // Set the text to transparent initially

        // Use DOTween to animate the text appearance
        _feedBackMessage.DOFade(1, 0.5f).OnComplete(() =>
        {
            // Fade out the message after a delay
            _feedBackMessage.DOFade(0, 1f).SetDelay(3.0f).OnComplete(() =>
            {
                feedbackBackground.SetActive(false);
            });

        });

    }

    private void CheckLevelCompletion()
    {
        if (UIManager.Instance.CardFaceScore >= 60)
        {
            CollectablesManager.instance.OnLevelUp();

            //LoadBatchBackground();
            UIManager.Instance.ResetScore(); // Optionally reset the score for the next level
            currentMoves = 0;
            remainingMoves = maxMoves;

            RefreshGame(); // Refresh the game when level is completed

        }
    }

    private void PlayBackgroundMusic()
    {
        if (backgroundMusic != null)
        {
            audioSource.clip = backgroundMusic;
            audioSource.loop = true;
            audioSource.Play();
        }
    }

    private void PlayFlipSound()
    {
        if (flipSound != null)
        {
            audioSource.PlayOneShot(flipSound);
        }
    }

    public void RefreshGame()
    {
        // Clear existing cards
        foreach (Transform child in gridTransform)
        {
            Destroy(child.gameObject);
        }
        // Load a new card batch and generate new cards
        LoadCardBatch();
        //LoadBatchBackground();

        heartAnimator.SetBool("Reset", true);
        heartAnimator.SetBool("isLow", false);
        heartAnimator.SetBool("isVeryLow", false);
        moveCounter.color = Color.green;
        moveCounter.text = remainingMoves.ToString();

        GenerateCards();

    }

    [System.Obsolete]
    public void RestartGame()
    {
        _newGame = true;
        CollectablesManager.instance.ResetCurrentLevel();
        currentMoves = 0;
        remainingMoves = maxMoves;

        UIManager.Instance.ResetScore(); // Optionally reset the score for the next level
        UIManager.Instance.SetScore(0);

        RefreshGame();
        gameOverbackground.SetActive(false);
        gameData.SaveGame(CollectablesManager.instance.CurrentLevel, UIManager.Instance.CurrentScore);

        Timer timer = FindObjectOfType<Timer>();
        timer.ResetTimer();
    }

    public void OpenMainMenu()
    {
        //RestartGame();
        currentMoves = 0;
        remainingMoves = maxMoves;
        RefreshGame();
        gameOverbackground.SetActive(false);
        mainMenu.SetActive(true);


    }

    [System.Obsolete]
    private void GameOver()
    {
        isGameOver = true;
        gameOverbackground.SetActive(true);

        lastLevel.text = "Level: " + CollectablesManager.instance.CurrentLevel.ToString();
        totalScore.text = UIManager.Instance.CurrentScore.ToString();
        UIManager.Instance.DisplayHighScore();

        Timer timer = FindObjectOfType<Timer>();
        if (timer != null)
        {
            finalTime.text = timer.DisplayFinalTime();
        }

        Time.timeScale = 0;


    }
}
