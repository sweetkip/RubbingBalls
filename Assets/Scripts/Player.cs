using UnityEngine;
using Fusion;
using TreeEditor;

public class Player : NetworkBehaviour
{
    private float speed = 10f;
    public override void FixedUpdateNetwork()
    {
        if(GetInput(out NetworkInputData data))
        {
            Vector3 movement = new Vector3(data.Direction.x, data.Direction.y, 0);
            transform.Translate(movement * speed * Runner.DeltaTime);
        }
    }
}
