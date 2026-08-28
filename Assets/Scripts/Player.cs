using UnityEngine;
using Fusion;

public class Player : NetworkBehaviour
{
    [SerializeField] private float speed = 10f;
    [SerializeField] private NetworkPrefabRef bullet;
    public override void FixedUpdateNetwork()
    {
        if(GetInput(out NetworkInputData data))
        {
            Vector3 movement = new Vector3(data.Direction.x, data.Direction.y, 0);
            transform.Translate(movement * speed * Runner.DeltaTime);
            if(data.Buttons.IsSet((int)InputButton.Fire))
            {
                if(Object.HasStateAuthority)
                {
                    Runner.Spawn(bullet, transform.position, Quaternion.identity);
                }
            }
        }
        if(!Object.HasInputAuthority)
            return;
        if(Input.GetKeyDown(KeyCode.T))
        {
            RPC_SayHello();
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SayHello()
    {
        Debug.Log("Hola");
    }
}
