using UnityEngine;

namespace BroSkater.Player
{
    // Basic input handler using Unity's old Input Manager.
    // Replace with Input System package if preferred.
    public class PlayerInputHandler : MonoBehaviour
    {
        public Vector2 MoveInput { get; private set; }
        public bool JumpInputDown { get; private set; } // True the frame jump is pressed
        public bool JumpInputHeld { get; private set; } // True while jump is held
        public bool JumpInputUp { get; private set; }   // True the frame jump is released
        public bool PushInputDown { get; private set; } // True the frame W/Up is pressed DOWN
        public bool BrakeInputHeld { get; private set; } // True while S/Down is held
        public bool GrindInputDown { get; private set; } // True the frame grind is pressed
        public bool GrindInputHeld { get; private set; } // True while grind is held
        // Add trick inputs
        public bool TrickInputDown { get; private set; } // Kickflip (True the frame trick input is pressed)
        public bool TrickAltInputDown { get; private set; } // Heelflip (True the frame alt trick input is pressed)
        public bool Special1InputDown { get; private set; } // 360 Flip (True the frame special1 input is pressed)

        void Update()
        {
            // Reset single-frame inputs
            JumpInputDown = false;
            JumpInputUp = false;
            PushInputDown = false;
            GrindInputDown = false;
            TrickInputDown = false;
            TrickAltInputDown = false;
            Special1InputDown = false;

            // Read movement axes
            MoveInput = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxisRaw("Vertical")); // Use GetAxisRaw for vertical to detect tap easier

            // Read jump button states
            if (Input.GetButtonDown("Jump"))
            {
                JumpInputDown = true;
                Debug.Log("[InputHandler] JumpInputDown SET to true");
            }
            JumpInputHeld = Input.GetButton("Jump");
            JumpInputUp = Input.GetButtonUp("Jump");

            // Read push input (Tap W/Up)
            if (Input.GetButtonDown("Vertical") && MoveInput.y > 0.1f) 
            { // Check it's the forward button press
                PushInputDown = true;
            }

            // Read brake input (Hold S/Down)
            BrakeInputHeld = MoveInput.y < -0.5f;

            // Read grind button
            GrindInputDown = Input.GetButtonDown("Grind");
            GrindInputHeld = Input.GetButton("Grind");
            
            // Read trick buttons
            TrickInputDown = Input.GetButtonDown("Fire1"); // Typically mapped to left ctrl/mouse button
            TrickAltInputDown = Input.GetButtonDown("Fire2"); // Typically mapped to left alt/right mouse button
            Special1InputDown = Input.GetButtonDown("Fire3"); // Typically mapped to left shift/middle mouse button

            // Consume inputs immediately if needed elsewhere, or let states consume them
        }

        // Optional: Methods to consume input after processing (States should call these)
        public void ConsumeJumpInputDown()
        {
            JumpInputDown = false;
            Debug.Log("[InputHandler] JumpInputDown CONSUMED (set to false)");
        }
        public void ConsumeJumpInputUp() => JumpInputUp = false;
        public void ConsumePushInputDown() => PushInputDown = false;
        public void ConsumeGrindInputDown() => GrindInputDown = false;
        public void ConsumeTrickInputDown() => TrickInputDown = false;
        public void ConsumeTrickAltInputDown() => TrickAltInputDown = false;
        public void ConsumeSpecial1InputDown() => Special1InputDown = false;
    }
} 