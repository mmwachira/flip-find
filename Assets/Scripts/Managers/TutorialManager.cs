using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using DG.Tweening; // Add this for DoTween

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager instance;

    public bool IsCheckingForMatch => _isCheckingForMatch;

    public GameObject cardPrefab;
    public Transform gridTransform;
    public Transform fullImageTransform; // Reference to the UI element to show full image
    public Image fullImageDisplay; // Image component to display the full image
    public Sprite[] cardImages;

    [SerializeField] private Animator heartAnimator;
    [SerializeField] private Button clickAnywhere;
    [SerializeField] private Button pauseButton; // Reference to the pause button
    [SerializeField] private Button backButton; // Reference to the back button
    [SerializeField] private GameObject tutorialPointer;
    [SerializeField] private GameObject tutorialMessage;

    [SerializeField] private GameObject pauseMenu; // Reference to the pause menu

    [SerializeField] private TMP_Text tutorialText;
    [SerializeField] private TMP_Text moveCounter;


    private Card firstFlippedCard;
    private Card secondFlippedCard;
    private int currentMoves = 0;
    private int remainingMoves;
    private const int maxMoves = 36;
    private Timer timer;

    [SerializeField] private bool isTutorialCompleted;
    [SerializeField] private AudioClip backgroundMusic; // Reference to the background music AudioClip
    [SerializeField] private AudioClip flipSound; // Reference to the card flip sound AudioClip
    private AudioSource audioSource; // AudioSource component to play the background music

    private bool isPaused = false; // To track the pause state
    private bool _isCheckingForMatch = false;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            //DontDestroyOnLoad(gameObject);
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

        pauseButton.onClick.AddListener(OnPauseButtonClicked);
        backButton.onClick.AddListener(OnBackButtonClicked);
    }

    void Start()
    {
        tutorialPointer.SetActive(true);
        tutorialMessage.SetActive(true);
        //tutorialText.text = "Welcome to FlipnFind! Click on the box to continue.";
        tutorialText.text = "Flip the indicated card";
        GenerateCards();
        PlayBackgroundMusic();
        remainingMoves = maxMoves;
        heartAnimator.SetBool("Reset", false);
        moveCounter.text = remainingMoves.ToString();

        // Initially hide the pause menu
        pauseMenu.SetActive(false);
    }

    [System.Obsolete]
    void Update()
    {
        if (!isPaused)
        {
            OnCardFlip();
        }
    }

    public void Quit()
    {
        Application.Quit();
    }

    public void GenerateCards()
    {
        List<Sprite> images = new List<Sprite>(cardImages);
        images.AddRange(cardImages); // Duplicate images for pairs
        images = ShuffleList(images);

        foreach (Sprite image in images)
        {
            GameObject cardObject = Instantiate(cardPrefab, gridTransform);
            Card card = cardObject.GetComponent<Card>();
            card.SetCardImage(image);
            CardAnim cardAnim = cardObject.GetComponent<CardAnim>();
            cardAnim.Init(card); // Initialize CardAnim with the Card reference
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

    public void OnCardFlipped(Card card)
    {
        if (_isCheckingForMatch)
            return; // Prevent flipping if a match check is in progress

        if (firstFlippedCard == null)
        {
            firstFlippedCard = card;
        }
        else if (secondFlippedCard == null)
        {
            secondFlippedCard = card;
            _isCheckingForMatch = true;
            StartCoroutine(CheckForMatch());
        }
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

        if (remainingMoves <= 0)
        {
            moveCounter.text = "0";
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

            // Update score
            UIManager.Instance.AddScore(10); // Add points for a correct match

            CheckTutorialCompletion();
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
        _isCheckingForMatch = false;
    }

    [System.Obsolete]
    private void OnCardFlip()
    {
        Card[] allCards = FindObjectsOfType<Card>();

        foreach (Card card in allCards)
        {
            if (card.IsFlipped)
            {
                tutorialPointer.SetActive(false);
                tutorialText.text = "Great! Now flip another card";

                if (card.IsMatched)
                {
                    isTutorialCompleted = true;
                    tutorialText.text = "Tutorial Complete! Flip and match all cards";
                    break;

                }
                if (!isTutorialCompleted)
                {
                    if (firstFlippedCard.cardFront != secondFlippedCard.cardFront)
                    {
                        tutorialText.text = "Incorrect. Try again!";
                        break;
                    }

                }
            }
        }
    }

    private void CheckTutorialCompletion()
    {
        if (UIManager.Instance.CurrentScore >= 60)
        {
            OnTutorialComplete();
        }

        if (AllCardsMatched())
        {
            RestartGame();
        }
    }

    private bool AllCardsMatched()
    {
        foreach (Transform child in gridTransform)
        {
            Card card = child.GetComponent<Card>();
            if (!card.IsMatched)
            {
                return false;
            }
        }
        return true;
    }

    private void OnTutorialComplete()
    {
        //isTutorialCompleted = true;
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainScene");
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

    public void clickAnywhereButtonClicked()
    {
        tutorialText.text = "This is the";
    }

    private void OnPauseButtonClicked()
    {
        if (!isPaused)
        {
            isPaused = true;
            StartCoroutine(PauseGameCoroutine());
        }
    }

    private void OnBackButtonClicked()
    {
        if (isPaused)
        {
            StartCoroutine(ResumeGameCoroutine());
        }
    }

    private IEnumerator PauseGameCoroutine()
    {
        // Animate the menu from left to right
        tutorialMessage.SetActive(false);
        pauseMenu.SetActive(true);
        pauseMenu.transform.DOMoveX(Screen.width / 2, 0.5f).SetEase(Ease.OutQuad);
        yield return new WaitForSecondsRealtime(0.5f); // Wait for the animation to complete
        Time.timeScale = 0f; // Pause the game
    }

    private IEnumerator ResumeGameCoroutine()
    {
        // Animate the menu from right to left
        pauseMenu.transform.DOMoveX(-Screen.width / 2, 0.5f).SetEase(Ease.OutQuad);
        yield return new WaitForSecondsRealtime(0.5f); // Wait for the animation to complete
        pauseMenu.SetActive(false);
        tutorialMessage.SetActive(true);
        Time.timeScale = 1f; // Resume the game
        isPaused = false;
    }

    private void RestartGame()
    {
        // Clear existing cards
        foreach (Transform child in gridTransform)
        {
            Destroy(child.gameObject);
        }
        // Generate new cards
        GenerateCards();
        // Reset timer
        timer.ResetTimer();
    }
}
