using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SekatoMasterGameEngine : MonoBehaviour
{
    public enum CharacterType { Sekato, Aika }

    [Header("=== CHARACTER SELECTION ===")]
    public CharacterType selectedCharacter = CharacterType.Sekato;
    public Transform playerTransform;
    public GameObject sekatoModel;
    public GameObject aikaModel;

    [Header("=== MOVEMENT & LANES ===")]
    public float forwardSpeed = 12f;
    public float maxSpeed = 30f;
    public float speedIncreaseRate = 0.15f;
    public float laneDistance = 3.5f; // Distance between Left (0), Center (1), Right (2)
    public float laneChangeSpeed = 18f;
    
    private int currentLane = 1; // 0 = Left, 1 = Center, 2 = Right
    private Vector3 targetPosition;

    [Header("=== JUMP & SLIDE PHYSICS ===")]
    public float jumpForce = 9f;
    public float gravity = -22f;
    private float verticalVelocity = 0f;
    private bool isGrounded = true;
    private bool isSliding = false;
    private Vector3 originalColliderSize;
    private Vector3 originalColliderCenter;
    public BoxCollider playerCollider;

    [Header("=== POWER-UPS & UPGRADES (LEVELS 0-5) ===")]
    // Upgrade levels for shop progression
    public int magnetLevel = 0;
    public int gliderLevel = 0;
    public int multiplierLevel = 0;
    public int jumpBoardLevel = 0;

    // Active power-up states
    public bool isMagnetActive = false;
    public bool isGliderActive = false;
    public int scoreMultiplier = 1;
    public float magnetRadius = 12f;

    [Header("=== ENEMY SYSTEM (FLYING DRONE) ===")]
    public Transform flyingDrone;
    public float droneFollowDistance = 6f;
    public float droneHoverHeight = 2.5f;

    [Header("=== ECONOMY, SCORE & REVIVE ===")]
    public int coinsCollected = 0;
    public float distanceTraveled = 0f;
    public int reviveCost = 20; // Escalates: 20 -> 40 -> 80 -> 160...
    public float highScore = 0f;

    [Header("=== UI ELEMENTS ===")]
    public Text distanceText;
    public Text coinText;
    public Text multiplierText;
    public GameObject gameOverPanel;
    public Text finalScoreText;
    public Text reviveCostText;

    private Vector2 swipeStartPosition;
    private bool isDead = false;

    void Start()
    {
        // Load High Score
        highScore = PlayerPrefs.GetFloat("SekatoRunner_HighScore", 0f);

        // Save original collider dimensions for sliding
        if (playerCollider != null)
        {
            originalColliderSize = playerCollider.size;
            originalColliderCenter = playerCollider.center;
        }

        // Initialize active character model
        SelectCharacter(selectedCharacter);

        reviveCost = 20;
        isDead = false;
    }

    void Update()
    {
        if (isDead) return;

        // 1. Distance & Speed Scaling
        forwardSpeed = Mathf.Min(forwardSpeed + (speedIncreaseRate * Time.deltaTime), maxSpeed);
        distanceTraveled += (forwardSpeed * Time.deltaTime) * scoreMultiplier;

        // Update UI Displays
        UpdateUI();

        // 2. Process Input (Swipes & Keyboard)
        HandleSwipeInput();
        HandleKeyboardInput();

        // 3. Lane Movement Calculation
        Vector3 currentPos = playerTransform.position;
        float targetX = (currentLane - 1) * laneDistance;
        float newX = Mathf.Lerp(currentPos.x, targetX, Time.deltaTime * laneChangeSpeed);

        // 4. Vertical Physics (Jumping & Glider Low-Gravity)
        float currentGravity = isGliderActive ? (gravity * 0.3f) : gravity;
        
        if (!isGrounded)
        {
            verticalVelocity += currentGravity * Time.deltaTime;
        }

        float newY = currentPos.y + (verticalVelocity * Time.deltaTime);

        // Ground Collision Check
        if (newY <= 0f)
        {
            newY = 0f;
            verticalVelocity = 0f;
            isGrounded = true;
        }

        // Apply Position
        float newZ = currentPos.z + (forwardSpeed * Time.deltaTime);
        playerTransform.position = new Vector3(newX, newY, newZ);

        // 5. Update Flying Drone Position behind player
        UpdateDronePosition();

        // 6. Coin Magnet Logic
        if (isMagnetActive)
        {
            ApplyMagnetEffect();
        }
    }

    // --- CHARACTER SELECTION ---
    public void SelectCharacter(CharacterType character)
    {
        selectedCharacter = character;
        if (sekatoModel != null) sekatoModel.SetActive(character == CharacterType.Sekato);
        if (aikaModel != null) aikaModel.SetActive(character == CharacterType.Aika);
    }

    // --- INPUT CONTROLS ---
    void HandleSwipeInput()
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began)
            {
                swipeStartPosition = touch.position;
            }
            else if (touch.phase == TouchPhase.Ended)
            {
                Vector2 swipeDelta = touch.position - swipeStartPosition;

                if (swipeDelta.magnitude > 40f) // Minimum swipe sensitivity
                {
                    if (Mathf.Abs(swipeDelta.x) > Mathf.Abs(swipeDelta.y))
                    {
                        // Horizontal Swipes
                        if (swipeDelta.x > 0 && currentLane < 2) currentLane++; // Swipe Right
                        else if (swipeDelta.x < 0 && currentLane > 0) currentLane--; // Swipe Left
                    }
                    else
                    {
                        // Vertical Swipes
                        if (swipeDelta.y > 0 && isGrounded) PerformJump(); // Swipe Up
                        else if (swipeDelta.y < 0 && !isSliding) StartCoroutine(PerformSlide()); // Swipe Down
                    }
                }
            }
        }
    }

    void HandleKeyboardInput()
    {
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
        {
            if (currentLane > 0) currentLane--;
        }
        else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
        {
            if (currentLane < 2) currentLane++;
        }
        else if ((Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.Space)) && isGrounded)
        {
            PerformJump();
        }
        else if ((Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) && !isSliding)
        {
            StartCoroutine(PerformSlide());
        }
    }

    void PerformJump()
    {
        isGrounded = false;
        verticalVelocity = jumpForce;
    }

    IEnumerator PerformSlide()
    {
        isSliding = true;

        // Duck down: reduce collider height
        if (playerCollider != null)
        {
            playerCollider.size = new Vector3(originalColliderSize.x, originalColliderSize.y * 0.5f, originalColliderSize.z);
            playerCollider.center = new Vector3(originalColliderCenter.x, originalColliderCenter.y * 0.5f, originalColliderCenter.z);
        }

        yield return new WaitForSeconds(0.85f);

        // Reset collider height
        if (playerCollider != null)
        {
            playerCollider.size = originalColliderSize;
            playerCollider.center = originalColliderCenter;
        }

        isSliding = false;
    }

    // --- POWER-UP SYSTEM ---
    float GetPowerUpDuration(int upgradeLevel)
    {
        // Base duration 5 seconds + 1 second per upgrade level (Level 0 = 5s, Level 5 = 10s)
        return 5f + upgradeLevel;
    }

    public void TriggerPowerUp(string type)
    {
        if (type == "Magnet") StartCoroutine(MagnetRoutine(GetPowerUpDuration(magnetLevel)));
        else if (type == "Glider") StartCoroutine(GliderRoutine(GetPowerUpDuration(gliderLevel)));
        else if (type == "Multiplier") StartCoroutine(MultiplierRoutine(GetPowerUpDuration(multiplierLevel)));
        else if (type == "JumpBoard") PerformJumpBoard();
    }

    IEnumerator MagnetRoutine(float duration)
    {
        isMagnetActive = true;
        yield return new WaitForSeconds(duration);
        isMagnetActive = false;
    }

    IEnumerator GliderRoutine(float duration)
    {
        isGliderActive = true;
        PerformJump(); // Launch into air
        yield return new WaitForSeconds(duration);
        isGliderActive = false;
    }

    IEnumerator MultiplierRoutine(float duration)
    {
        scoreMultiplier = 2;
        yield return new WaitForSeconds(duration);
        scoreMultiplier = 1;
    }

    void PerformJumpBoard()
    {
        isGrounded = false;
        verticalVelocity = jumpForce * 1.6f; // Super high jump
    }

    void ApplyMagnetEffect()
    {
        Collider[] hitColliders = Physics.OverlapSphere(playerTransform.position, magnetRadius);
        foreach (var col in hitColliders)
        {
            if (col.CompareTag("Coin"))
            {
                col.transform.position = Vector3.MoveTowards(col.transform.position, playerTransform.position, Time.deltaTime * 25f);
            }
        }
    }

    // --- DRONE ENEMY MOVEMENT ---
    void UpdateDronePosition()
    {
        if (flyingDrone != null)
        {
            Vector3 droneTarget = playerTransform.position - new Vector3(0, -droneHoverHeight, droneFollowDistance);
            flyingDrone.position = Vector3.Lerp(flyingDrone.position, droneTarget, Time.deltaTime * 4f);
        }
    }

    // --- COLLISIONS & TRIGGERS ---
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Coin"))
        {
            coinsCollected++;
            Destroy(other.gameObject);
        }
        else if (other.CompareTag("Obstacle"))
        {
            TriggerGameOver();
        }
        else if (other.CompareTag("PowerUp_Magnet"))
        {
            TriggerPowerUp("Magnet");
            Destroy(other.gameObject);
        }
        else if (other.CompareTag("PowerUp_Glider"))
        {
            TriggerPowerUp("Glider");
            Destroy(other.gameObject);
        }
        else if (other.CompareTag("PowerUp_Multiplier"))
        {
            TriggerPowerUp("Multiplier");
            Destroy(other.gameObject);
        }
        else if (other.CompareTag("PowerUp_JumpBoard"))
        {
            TriggerPowerUp("JumpBoard");
            Destroy(other.gameObject);
        }
    }

    // --- UI & GAME OVER SYSTEM ---
    void UpdateUI()
    {
        if (distanceText != null) distanceText.text = (distanceTraveled / 10f).ToString("F1") + " km";
        if (coinText != null) coinText.text = coinsCollected.ToString();
        if (multiplierText != null) multiplierText.text = scoreMultiplier > 1 ? "2x ACTIVE!" : "";
    }

    public void TriggerGameOver()
    {
        isDead = true;

        // Check & Save High Score
        if (distanceTraveled > highScore)
        {
            highScore = distanceTraveled;
            PlayerPrefs.SetFloat("SekatoRunner_HighScore", highScore);
            PlayerPrefs.Save();
        }

        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        if (finalScoreText != null) finalScoreText.text = "Distance: " + (distanceTraveled / 10f).ToString("F1") + " km";
        if (reviveCostText != null) reviveCostText.text = "Revive Cost: " + reviveCost + " Coins";
    }

    public void RevivePlayer()
    {
        if (coinsCollected >= reviveCost)
        {
            coinsCollected -= reviveCost;
            reviveCost *= 2; // Doubling cost on each revive (20 -> 40 -> 80...)
            isDead = false;

            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            
            // Push player forward slightly and grant temporary safety jump
            PerformJump();
        }
    }
}
