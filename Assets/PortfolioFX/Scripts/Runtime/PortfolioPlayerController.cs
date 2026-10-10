using UnityEngine;
namespace PortfolioFX
{
    [DisallowMultipleComponent]
    public sealed class PortfolioPlayerController : MonoBehaviour
    {
        public Camera targetCamera;public float moveSpeed=3.8f,lookSpeed=3.2f,gravity=-18,jumpHeight=1.2f;public bool enabledInput=true;
        CharacterController cc;Animator animator;FXCharacterMotion motion;Vector3 velocity;float yaw,pitch;int cameraMode=1;
        void Awake(){cc=GetComponent<CharacterController>();animator=GetComponentInChildren<Animator>();motion=GetComponent<FXCharacterMotion>();if(targetCamera)yaw=targetCamera.transform.eulerAngles.y;}
        void Update(){if(!enabledInput)return;HandleActions();if(Input.GetKeyDown(KeyCode.Tab)){cameraMode=(cameraMode+1)%3;ApplyCameraMode();}if(Input.GetMouseButtonDown(3)){cameraMode=(cameraMode+1)%3;ApplyCameraMode();}if(Input.GetMouseButtonDown(4)){cameraMode=(cameraMode+2)%3;ApplyCameraMode();}if(cameraMode==0)Move();else Move();}
        void Move(){float h=Input.GetAxisRaw("Horizontal"),v=Input.GetAxisRaw("Vertical");Vector3 forward=targetCamera?Vector3.Scale(targetCamera.transform.forward,new Vector3(1,0,1)).normalized:transform.forward;Vector3 right=targetCamera?targetCamera.transform.right:transform.right;Vector3 input=(forward*v+right*h);if(input.sqrMagnitude>1)input.Normalize();if(input.sqrMagnitude>.01f){transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(input),Time.deltaTime*10);}
            if(cc){if(cc.isGrounded&&velocity.y<0)velocity.y=-1;if(Input.GetButtonDown("Jump")&&cc.isGrounded)velocity.y=Mathf.Sqrt(jumpHeight*-2*gravity);velocity.y+=gravity*Time.deltaTime;cc.Move((input*moveSpeed+velocity)*Time.deltaTime);}else transform.position+=input*moveSpeed*Time.deltaTime;
            if(animator){animator.SetFloat("Speed",input.magnitude);animator.SetFloat("MoveSpeed",input.magnitude);}
            // FXCharacterMotion is a manually sampled PlayableGraph (the
            // imported Animator Controller is intentionally empty). Advance
            // through Ellen's authored run cue while the player is moving.
            if(motion && input.sqrMagnitude>.01f)motion.Sample(1.1f+Mathf.Repeat(Time.time,1.05f));
            if(targetCamera&&cameraMode==1){yaw+=Input.GetAxis("Mouse X")*lookSpeed;pitch=Mathf.Clamp(pitch-Input.GetAxis("Mouse Y")*lookSpeed,-20,35);targetCamera.transform.position=transform.position+Quaternion.Euler(pitch,yaw,0)*new Vector3(0,2.2f,-5.2f);targetCamera.transform.LookAt(transform.position+Vector3.up*1.2f);}else if(targetCamera&&cameraMode==2){targetCamera.transform.position=transform.position+new Vector3(0,7,-.1f);targetCamera.transform.LookAt(transform.position+transform.forward*3);}
        }
        void HandleActions(){KeyCode[] keys={KeyCode.Q,KeyCode.E,KeyCode.R,KeyCode.T};for(int i=0;i<keys.Length;i++)if(Input.GetKeyDown(keys[i])){if(animator){animator.SetInteger("PortfolioAction",i+1);animator.SetTrigger("Action");}if(motion)motion.Sample(i==0?1.3f:i==1?1.95f:i==2?2.8f:3.2f);}}
        void ApplyCameraMode(){if(!targetCamera)return;targetCamera.fieldOfView=cameraMode==0?52:cameraMode==1?58:44;}
        public void SetInput(bool value){enabledInput=value;}
    }
}
