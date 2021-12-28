using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Содержит команду, которую пользователь желает персонажу.
struct Cmd
{
    public float forwardMove;
    public float rightMove;
    public float upMove;
}

public class CPMPlayer : MonoBehaviour
{
    public Transform playerView;     // Камера
    public float playerViewYOffset = 0.6f; // Высота, на которой камера привязана к
    public float xMouseSensitivity = 30.0f;
    public float yMouseSensitivity = 30.0f;
    //
    /*Факторы возникновения кадра*/
    public float gravity = 20.0f;

    public float friction = 6; //Трение о поверхность

    /* Движение */
    public float moveSpeed = 7.0f;                // Скорость по земле
    public float runAcceleration = 14.0f;         // Ускорение по земле
    public float runDeacceleration = 10.0f;       // Торможение при беге по земле
    public float airAcceleration = 2.0f;          // Ускорение в воздухе
    public float airDecceleration = 2.0f;         // Произошло замедление при обстреле другого объекта
    public float airControl = 0.3f;               // Насколько точен контроль воздуха
    public float sideStrafeAcceleration = 50.0f;  // Насколько быстро происходит ускорение, чтобы встать на сторону
    public float sideStrafeSpeed = 1.0f;          // Какая максимальная скорость генерируется при боковом обстреле
    public float jumpSpeed = 8.0f;                // Скорость, с которой увеличивается верхняя ось персонажа при выполнении прыжка.
    public bool holdJumpToBhop = false;           // Когда этот параметр включен, позволяет игроку просто удерживать кнопку прыжка, чтобы продолжать двигаться идеально.

    /*print() style */
    public GUIStyle style;

    /*FPS Stuff */
    public float fpsDisplayRate = 4.0f; // 4 updates per sec

    private int frameCount = 0;
    private float dt = 0.0f;
    private float fps = 0.0f;

    private CharacterController _controller;

    // Camera rotations
    private float rotX = 0.0f;
    private float rotY = 0.0f;

    private Vector3 moveDirectionNorm = Vector3.zero;
    private Vector3 playerVelocity = Vector3.zero;
    private float playerTopVelocity = 0.0f;

    // Q3: игроки могут поставить в очередь следующий прыжок незадолго до того, как он упадет на землю
    private bool wishJump = false;

    // Используется для отображения значений фриктона в реальном времени
    private float playerFriction = 0.0f;

    // Команды игрока, хранит команды желаний, которые запрашивает игрок (вперед, назад, прыжок и т. Д.)
    private Cmd _cmd;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (playerView == null)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
                playerView = mainCamera.gameObject.transform;
        }

        // Поместите камеру внутрь капсульного коллайдера.
        playerView.position = new Vector3(
            transform.position.x,
            transform.position.y + playerViewYOffset,
            transform.position.z);

        _controller = GetComponent<CharacterController>();
    }

    private void Update()
    {
        // при перемещении координат ниже 100, сцена перезапускается;
        if (transform.position.y < -100f)
        {
            Application.LoadLevel(Application.loadedLevel);
        }

        if (Input.GetKeyDown(KeyCode.I))
        {
            Application.LoadLevel(Application.loadedLevel);
        }

        // Счётчик FPS
        frameCount++;
        dt += Time.deltaTime;
        if (dt > 1.0 / fpsDisplayRate)
        {
            fps = Mathf.Round(frameCount / dt);
            frameCount = 0;
            dt -= 1.0f / fpsDisplayRate;
        }

        /* Вращение камеры, мышь управляет этим */
        rotX -= Input.GetAxisRaw("Mouse Y") * xMouseSensitivity * 0.02f;
        rotY += Input.GetAxisRaw("Mouse X") * yMouseSensitivity * 0.02f;

        // Зажмите поворот по оси X
        if (rotX < -90)
            rotX = -90;
        else if (rotX > 90)
            rotX = 90;

        this.transform.rotation = Quaternion.Euler(0, rotY, 0); // Вращает коллайдер
        playerView.rotation = Quaternion.Euler(rotX, rotY, 0); // Поворачивает камеру



        /* Движение, вот важная часть */
        QueueJump();
        if (_controller.isGrounded)
            GroundMove();
        else if (!_controller.isGrounded)
            AirMove();

        // Переместите контроллер
        _controller.Move(playerVelocity * Time.deltaTime);

        /* Рассчитать максимальную скорость */
        Vector3 udp = playerVelocity;
        udp.y = 0.0f;
        if (udp.magnitude > playerTopVelocity)
            playerTopVelocity = udp.magnitude;

        // Необходимо переместить камеру после того, как игрок был перемещен, потому что в противном случае камера будет обрезать игрока, если будет двигаться достаточно быстро, и всегда будет отставать на 1 кадр.
        // Устанавливаем положение камеры для преобразования
        playerView.position = new Vector3(
            transform.position.x,
            transform.position.y + playerViewYOffset,
            transform.position.z);
    }

    /*******************************************************************************************************\
   |* MOVEMENT
   \*******************************************************************************************************/

    /**
     * Устанавливает направление движения в зависимости от ввода игрока
     */
    private void SetMovementDir()
    {
        _cmd.forwardMove = Input.GetAxisRaw("Vertical");
        _cmd.rightMove = Input.GetAxisRaw("Horizontal");
    }

    /**
     * Ставит в очередь следующий прыжок, как в третьем квейке
     */
    private void QueueJump()
    {
        if (holdJumpToBhop)
        {
            wishJump = Input.GetButton("Jump");
            return;
        }

        if (Input.GetButtonDown("Jump") && !wishJump)
            wishJump = true;
        if (Input.GetButtonUp("Jump"))
            wishJump = false;
    }

    /**
     * Выполняется, когда игрок находится в воздухе
    */
    private void AirMove()
    {
        Vector3 wishdir;
        float wishvel = airAcceleration;
        float accel;

        SetMovementDir();

        wishdir = new Vector3(_cmd.rightMove, 0, _cmd.forwardMove);
        wishdir = transform.TransformDirection(wishdir);

        float wishspeed = wishdir.magnitude;
        wishspeed *= moveSpeed;

        wishdir.Normalize();
        moveDirectionNorm = wishdir;

        // CPM: Aircontrol
        float wishspeed2 = wishspeed;
        if (Vector3.Dot(playerVelocity, wishdir) < 0)
            accel = airDecceleration;
        else
            accel = airAcceleration;
        // Если игрок стрейфится ТОЛЬКО влево или вправо
        if (_cmd.forwardMove == 0 && _cmd.rightMove != 0)
        {
            if (wishspeed > sideStrafeSpeed)
                wishspeed = sideStrafeSpeed;
            accel = sideStrafeAcceleration;
        }

        Accelerate(wishdir, wishspeed, accel);
        if (airControl > 0)
            AirControl(wishdir, wishspeed2);
        // !CPM: Контроль в воздухе

        // Apply gravity
        playerVelocity.y -= gravity * Time.deltaTime;
    }

    /**
     * Контроль воздуха происходит, когда игрок находится в воздухе, это позволяет
     * игрокам перемещаться из стороны в сторону намного быстрее, чем
     * «вялый» в поворотах.
     */
    private void AirControl(Vector3 wishdir, float wishspeed)
    {
        float zspeed;
        float speed;
        float dot;
        float k;

        // Не может контролировать движение, если не движется вперед или назад
        if (Mathf.Abs(_cmd.forwardMove) < 0.001 || Mathf.Abs(wishspeed) < 0.001)
            return;
        zspeed = playerVelocity.y;
        playerVelocity.y = 0;
        /* Следующие две строки эквивалентны VectorNormalize от idTech.() */
        speed = playerVelocity.magnitude;
        playerVelocity.Normalize();

        dot = Vector3.Dot(playerVelocity, wishdir);
        k = 32;
        k *= airControl * dot * dot * Time.deltaTime;

        // Изменить направление при замедлении
        if (dot > 0)
        {
            playerVelocity.x = playerVelocity.x * speed + wishdir.x * k;
            playerVelocity.y = playerVelocity.y * speed + wishdir.y * k;
            playerVelocity.z = playerVelocity.z * speed + wishdir.z * k;

            playerVelocity.Normalize();
            moveDirectionNorm = playerVelocity;
        }

        playerVelocity.x *= speed;
        playerVelocity.y = zspeed; // Note this line
        playerVelocity.z *= speed;
    }

    /**
     * Вызывается каждый кадр, когда движок определяет, что игрок находится на земле.
     */
    private void GroundMove()
    {
        Vector3 wishdir;

        // Не применяйте трение, если игрок стоит в очереди для следующего прыжка.
        if (!wishJump)
            ApplyFriction(1.0f);
        else
            ApplyFriction(0);

        SetMovementDir();

        wishdir = new Vector3(_cmd.rightMove, 0, _cmd.forwardMove);
        wishdir = transform.TransformDirection(wishdir);
        wishdir.Normalize();
        moveDirectionNorm = wishdir;

        var wishspeed = wishdir.magnitude;
        wishspeed *= moveSpeed;

        Accelerate(wishdir, wishspeed, runAcceleration);

        // Сбросить скорость гравитации
        playerVelocity.y = -gravity * Time.deltaTime;

        if (wishJump)
        {
            playerVelocity.y = jumpSpeed;
            wishJump = false;
        }
    }

    /**
     * Применяет трение к игроку, вызываемое как в воздухе, так и на земле
     */
    private void ApplyFriction(float t)
    {
        Vector3 vec = playerVelocity; // Эквивалентен: VectorCopy();
        float speed;
        float newspeed;
        float control;
        float drop;

        vec.y = 0.0f;
        speed = vec.magnitude;
        drop = 0.0f;

        /* Только если игрок находится на земле, примените трение */
        if (_controller.isGrounded)
        {
            control = speed < runDeacceleration ? runDeacceleration : speed;
            drop = control * friction * Time.deltaTime * t;
        }

        newspeed = speed - drop;
        playerFriction = newspeed;
        if (newspeed < 0)
            newspeed = 0;
        if (speed > 0)
            newspeed /= speed;

        playerVelocity.x *= newspeed;
        playerVelocity.z *= newspeed;
    }

    private void Accelerate(Vector3 wishdir, float wishspeed, float accel)
    {
        float addspeed;
        float accelspeed;
        float currentspeed;

        currentspeed = Vector3.Dot(playerVelocity, wishdir);
        addspeed = wishspeed - currentspeed;
        if (addspeed <= 0)
            return;
        accelspeed = accel * Time.deltaTime * wishspeed;
        if (accelspeed > addspeed)
            accelspeed = addspeed;

        playerVelocity.x += accelspeed * wishdir.x;
        playerVelocity.z += accelspeed * wishdir.z;
    }

    private void OnGUI()
    {
        GUI.Label(new Rect(0, 0, 400, 100), "FPS: " + fps, style);
        var ups = _controller.velocity;
        ups.y = 0;
        GUI.Label(new Rect(0, 15, 400, 100), "Speed: " + Mathf.Round(ups.magnitude * 100) / 100 + " ms", style);
        GUI.Label(new Rect(0, 30, 400, 100), "Max Speed: " + Mathf.Round(playerTopVelocity * 100) / 100 + " ms", style);
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        switch (hit.gameObject.tag)
        {
            case "HithJumpBoost":
                jumpSpeed = 20f;
                break;
            case "LowJumpBoost":
                jumpSpeed = 10f;
                break;
            case "Ground":
                jumpSpeed = 8f;
                break;
        }
    }
}
