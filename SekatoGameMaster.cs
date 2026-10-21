using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// Data structure for Leaderboard Entry
[System.Serializable]
public class LeaderboardEntry
{
    public string playerName;
    public float highScoreKm;

    public LeaderboardEntry(string name, float score)
    {
        this.playerName = name;
        this.highScoreKm = score;
    }
}

public class SekatoGameMaster : MonoBehaviour
{
    [Header("--- PLAYER DATA & PROFILE ---")]
    public string currentPlayerName = "SekatoPlayer";
    public string playerEmojiAvatar = "🥷";
    private float personalBestScore = 0f;

    [Header("--- MOVEMENT & 3 LANES ---")]
    public float forwardSpeed = 6f;
    public float maxSpeed = 22f;
    public float laneDistance = 3f;
    public float jumpForce = 9f;
    public float boardJumpForce = 15f; // Extra jump height for Jump Board
    public float gravity = -22f;

    private CharacterController controller;
    private Animator animator;
    private Vector3 direction;
    private int currentLane = 1; // 0 = Left, 1 = Center, 2 = Right
    private bool hasJumpBoard = false;

    // Mobile Swipe Gesture Variables
    private Vector2 swipeStartPosition;
    private Vector2 swipeEndPosition;
    private float minSwipeDistance = 50f;

    [Header("--- POWER-UPS & UPGRADES ---")]
    public int magnetLevel = 0;   // Lv 0 (5s) to Lv 5 (10s)
    public int gliderLevel = 0;
    public int starLevel = 0;

    private bool isMagnetActive = false;
    private bool isGliderActive = false;
    private int scoreMultiplier = 1;
    public float magnetRadius = 12f;
    public LayerMask coinLayer;

    [Header("--- GAME STATS & CURRENCY ---")]
    public float distanceKm = 0f;
    public int totalCoins = 0;
    public int currentCoinMultiplier = 1;

    [Header("--- REVIVE SYSTEM ---")]
    public int baseReviveCost = 20;
    private int currentReviveCost;
    public GameObject reviveUI;
    public Text reviveCostText;

    [Header("--- CONGRATULATIONS HIGH SCORE UI ---")]
    public GameObject highScoreUI; // High Score Popup Window
    public Text highScoreText;
    public Text characterEmojiText; // Displays "👍 Real Sekato Pose 😉"

    [Header("--- LEADERBOARD SYSTEM ---")]
    public GameObject leaderboardPanel;
    public Transform leaderboardContentArea;
    public GameObject leaderboardRowPrefab;
    public static List<LeaderboardEntry> globalLeaderboard = new List<LeaderboardEntry>();

    [Header("--- UI ELEMENTS ---")]
    public Text distanceText;
    public Text coinText;
    public GameObject powerUpTimerUI;
    public Text powerUpTimerText;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        currentReviveCost = baseReviveCost;

        // Hide UI Popups at start
        if (reviveUI) reviveUI.SetActive(false);
        if (highScoreUI) highScoreUI.SetActive(false);
        if (powerUpTimerUI) powerUpTimerUI.SetActive(false);
        if (leaderboardPanel) leaderboardPanel.SetActive(false);

        // Load saved personal high score from phone storage
        personalBestScore = PlayerPrefs.GetFloat("PersonalBest", 0f);

        // Lock frame rate to 60 FPS for lag-free performance
        Application.targetFrameRate = 60;

        InitializeSampleLeaderboard();
    }

    void Update()
    {
        // Pause gameplay if any menu window is open
        if ((reviveUI && reviveUI.activeSelf) || 
            (highScoreUI && highScoreUI.activeSelf) || 
            (leaderboardPanel && leaderboardPanel.activeSelf)) return;

        // 1. DYNAMIC SPEED & DISTANCE SCALING
        if (forwardSpeed < maxSpeed)
        {
            forwardSpeed += Time.deltaTime * 0.1f;
        }

        distanceKm += (forwardSpeed * Time.deltaTime * scoreMultiplier) / 100f;
        if (distanceText) distanceText.text = distanceKm.ToString("F1") + " km";

        // Dynamic Coin Multiplier based on distance
        if (distanceKm < 2f) currentCoinMultiplier = 1;
        else if (distanceKm < 5f) currentCoinMultiplier = 3;
        else if (distanceKm < 10f) currentCoinMultiplier = 5;
        else currentCoinMultiplier = 7;

        // 3D Run Animation Speed Sync
        if (animator)
        {
            float speedRatio = forwardSpeed / maxSpeed;
            animator.SetFloat("Speed", speedRatio);
        }

        // 2. INPUT DETECTION (Keyboard + Touch Swiping)
        HandleInputs();

        // Calculate Lane Positions (Lane 0: -3, Lane 1: 0, Lane 2: +3)
        Vector3 targetPosition = transform.position;
        if (currentLane == 0) targetPosition.x = -laneDistance;
        else if (currentLane == 1) targetPosition.x = 0;
        else if (currentLane == 2) targetPosition.x = laneDistance;

        Vector3 moveSide = targetPosition - transform.position;
        direction.x = moveSide.x * 12f;
        direction.z = forwardSpeed;

        // 3. JUMP & SLIDE GRAVITY
        if (controller.isGrounded)
        {
            direction.y = -1f;
        }
        else
        {
            if (isGliderActive)
            {
                direction.y = 0f; // Levitating mid-air
            }
            else
            {
                direction.y += gravity * Time.deltaTime;
            }
        }

        // Apply Player Movement
        controller.Move(direction * Time.deltaTime);

        // 4. MAGNET POWER-UP LOGIC
        if (isMagnetActive)
        {
            Collider[] coins = Physics.OverlapSphere(transform.position, magnetRadius, coinLayer);
            foreach (Collider coin in coins)
            {
                coin.transform.position = Vector3.MoveTowards(coin.transform.position, transform.position, 18f * Time.deltaTime);
            }
        }

        // 5. FALL DETECTION
        if (transform.position.y < -5f)
        {
            TriggerHitOrGameOver();
        }
    }

    // --- SWIPE & KEYBOARD INPUT CONTROLS ---
    void HandleInputs()
    {
        // Keyboard Controls
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) SwipeRight();
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) SwipeLeft();
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.UpArrow)) Jump();
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) Slide();

        // Mobile Touch Screen Controls (Swiping)
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began)
            {
                swipeStartPosition = touch.position;
            }
            else if (touch.phase == TouchPhase.Ended)
            {
                swipeEndPosition = touch.position;
                Vector2 swipeVector = swipeEndPosition - swipeStartPosition;

                if (swipeVector.magnitude >= minSwipeDistance)
                {
                    swipeVector.Normalize();

                    // Horizontal Swipes (Lane Change)
                    if (Mathf.Abs(swipeVector.x) > Mathf.Abs(swipeVector.y))
                    {
                        if (swipeVector.x > 0) SwipeRight();
                        else SwipeLeft();
                    }
                    // Vertical Swipes (Jump / Slide)
                    else
                    {
                        if (swipeVector.y > 0) Jump();
                        else Slide();
                    }
                }
            }
        }
    }

    void SwipeRight()
    {
        if (currentLane < 2) currentLane++;
    }

    void SwipeLeft()
    {
        if (currentLane > 0) currentLane--;
    }

    void Jump()
    {
        if (controller.isGrounded)
        {
            float currentJump = hasJumpBoard ? boardJumpForce : jumpForce;
            direction.y = currentJump;

            if (animator) animator.SetTrigger("Jump");

            hasJumpBoard = false;
        }
    }

    void Slide()
    {
        if (controller.isGrounded)
        {
            StartCoroutine(SlideRoutine());
        }
    }

    IEnumerator SlideRoutine()
    {
        if (animator) animator.SetTrigger("Slide");
        controller.height = 1.0f;
        yield return new WaitForSeconds(1.0f);
        controller.height = 2.0f;
    }

    // --- TRIGGER & ITEM INTERACTIONS ---
    private void OnTriggerEnter(Collider other)
    {
        // Coins
        if (other.CompareTag("Coin"))
        {
            totalCoins += currentCoinMultiplier;
            if (coinText) coinText.text = "$ " + totalCoins;
            Destroy(other.gameObject);
        }

        // Rare Mystery Box
        if (other.CompareTag("MysteryBox"))
        {
            totalCoins += 10;
            if (coinText) coinText.text = "$ " + totalCoins;
            Destroy(other.gameObject);
        }

        // Jump Board Power-Up
        if (other.CompareTag("JumpBoard"))
        {
            hasJumpBoard = true;
            Destroy(other.gameObject);
        }

        // Magnet Power-Up
        if (other.CompareTag("MagnetItem"))
        {
            float duration = 5f + magnetLevel;
            StartCoroutine(ActivateMagnet(duration));
            Destroy(other.gameObject);
        }

        // Glider Power-Up
        if (other.CompareTag("GliderItem"))
        {
            float duration = 5f + gliderLevel;
            StartCoroutine(ActivateGlider(duration));
            Destroy(other.gameObject);
        }

        // 2x Star Multiplier
        if (other.CompareTag("StarItem"))
        {
            float duration = 5f + starLevel;
            StartCoroutine(ActivateStar(duration));
            Destroy(other.gameObject);
        }

        // Obstacles or Robot Shots
        if (other.CompareTag("Obstacle") || other.CompareTag("TallObstacle") || other.CompareTag("RobotShot"))
        {
            TriggerHitOrGameOver();
        }
    }

    // --- POWER-UP COROUTINES ---
    IEnumerator ActivateMagnet(float duration)
    {
        isMagnetActive = true;
        yield return StartCoroutine(DisplayPowerUpTimer(duration));
        isMagnetActive = false;
    }

    IEnumerator ActivateGlider(float duration)
    {
        isGliderActive = true;
        transform.position = new Vector3(transform.position.x, 4.5f, transform.position.z);
        yield return StartCoroutine(DisplayPowerUpTimer(duration));
        isGliderActive = false;
    }

    IEnumerator ActivateStar(float duration)
    {
        scoreMultiplier = 2;
        yield return StartCoroutine(DisplayPowerUpTimer(duration));
        scoreMultiplier = 1;
    }

    IEnumerator DisplayPowerUpTimer(float duration)
    {
        if (powerUpTimerUI) powerUpTimerUI.SetActive(true);
        float timeRemaining = duration;

        while (timeRemaining > 0)
        {
            timeRemaining -= Time.deltaTime;
            if (powerUpTimerText) powerUpTimerText.text = Mathf.CeilToInt(timeRemaining) + "s";
            yield return null;
        }

        if (powerUpTimerUI) powerUpTimerUI.SetActive(false);
    }

    // --- REVIVE & HIGH SCORE CONDITIONAL CHECK ---
    public void TriggerHitOrGameOver()
    {
        if (animator) animator.SetTrigger("Hit");
        if (reviveUI)
        {
            reviveUI.SetActive(true);
            if (reviveCostText) reviveCostText.text = "Revive for " + currentReviveCost + " Coins?";
        }
    }

    public void RevivePlayer()
    {
        if (totalCoins >= currentReviveCost)
        {
            totalCoins -= currentReviveCost;
            if (coinText) coinText.text = "$ " + totalCoins;

            currentReviveCost *= 2;
            if (reviveUI) reviveUI.SetActive(false);

            transform.position = new Vector3(0, 1, transform.position.z + 5f);
        }
    }

    public void CancelAndRestart()
    {
        if (reviveUI) reviveUI.SetActive(false);

        // CHECK SCORE CONDITION
        if (distanceKm > personalBestScore)
        {
            // NEW HIGH SCORE! Save it & show congratulations screen
            personalBestScore = distanceKm;
            PlayerPrefs.SetFloat("PersonalBest", personalBestScore);

            if (highScoreUI)
            {
                highScoreUI.SetActive(true);
                if (highScoreText) highScoreText.text = "NEW HIGH SCORE!\n" + distanceKm.ToString("F1") + " km";
                if (characterEmojiText) characterEmojiText.text = "Sekato says: \nCONGRATULATIONS! 👍😉";
                if (animator) animator.SetTrigger("Celebrate");
            }
        }
        else
        {
            // LOWER SCORE: Skip popup immediately and return to Lobby!
            FinishAndReloadScene();
        }
    }

    public void FinishAndReloadScene()
    {
        SubmitScoreToLeaderboard(currentPlayerName, distanceKm);
        currentReviveCost = baseReviveCost;
        
        // Reload scene (Returns player to the Main Lobby)
        SceneManager.LoadScene("MainLobbyScene");
    }

    // --- DYNAMIC GLOBAL LEADERBOARD ---
    void InitializeSampleLeaderboard()
    {
        if (globalLeaderboard.Count == 0)
        {
            globalLeaderboard.Add(new LeaderboardEntry("Friend1", 1002f));
            globalLeaderboard.Add(new LeaderboardEntry("AikaRunner", 850f));
            globalLeaderboard.Add(new LeaderboardEntry("ProParkour", 500f));
        }
    }

    public void SubmitScoreToLeaderboard(string name, float score)
    {
        LeaderboardEntry existingEntry = globalLeaderboard.Find(e => e.playerName == name);

        if (existingEntry != null)
        {
            if (score > existingEntry.highScoreKm)
            {
                existingEntry.highScoreKm = score;
            }
        }
        else
        {
            globalLeaderboard.Add(new LeaderboardEntry(name, score));
        }

        globalLeaderboard.Sort((a, b) => b.highScoreKm.CompareTo(a.highScoreKm));
    }

    public void OpenLeaderboardUI()
    {
        SubmitScoreToLeaderboard(currentPlayerName, distanceKm);
        if (leaderboardPanel) leaderboardPanel.SetActive(true);

        foreach (Transform child in leaderboardContentArea)
        {
            Destroy(child.gameObject);
        }

        for (int i = 0; i < globalLeaderboard.Count; i++)
        {
            GameObject row = Instantiate(leaderboardRowPrefab, leaderboardContentArea);

            Text rankText = row.transform.GetChild(0).GetComponent<Text>();
            Text nameText = row.transform.GetChild(1).GetComponent<Text>();
            Text scoreText = row.transform.GetChild(2).GetComponent<Text>();

            rankText.text = "#" + (i + 1);
            nameText.text = globalLeaderboard[i].playerName;
            scoreText.text = globalLeaderboard[i].highScoreKm.ToString("F1") + " km";
        }
    }

    public void CloseLeaderboardUI()
    {
        if (leaderboardPanel) leaderboardPanel.SetActive(false);
    }
}
