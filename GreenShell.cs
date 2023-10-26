using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class GreenShellScript : MonoBehaviour
{
    // Start is called before the first frame update
    [SerializeField] float speed = 1.0f;
    [SerializeField] int maxWallHits;
    [SerializeField] float timeUntilDestroyed = 5.0f;
    [SerializeField] float timePlayerInputDeactivatedAfterCollision = 1.0f;
    [SerializeField] float hitPower = 5.0f;
    float radius;
    int wallHits;

    // NOTE: THIS IS THE SAME AS THE RED SHELL SCRIPT BUT INSTEAD OF FOLLOWING THE NEXT PLAYER AND BREAKING ON WALLS, IT BOUNCES OF WALLS
    
    void Start()
    {
        radius = GetComponent<SphereCollider>().radius;
        // queue destorying self after a certain time
        Invoke("DestroySelf", timeUntilDestroyed);
    }

    // Update is called once per frame
    void Update()
    {
        // move forward
        transform.Translate(transform.forward * speed * Time.deltaTime, Space.World);
        // bounce of walls
        RaycastHit hit;
        if(Physics.Raycast(transform.position, transform.forward, out hit, radius/4.0f)){
            if (hit.collider.gameObject.tag == "Wall")
            {
                transform.forward = Vector3.Reflect(transform.forward, hit.normal);
                wallHits += 1;
            }
        }
        if(wallHits > maxWallHits) {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // deal with collision with racers, destroy
        if (other.gameObject.tag == "Player")
        {
            CarController script = other.transform.parent.GetComponent<CarController>();
            script.DealWithShellCollision(timePlayerInputDeactivatedAfterCollision, transform.forward * hitPower);
            Destroy(gameObject);
        }
        if (other.gameObject.tag == "AI")
        {
            AICarController script = other.transform.parent.GetComponent<AICarController>();
            script.DealWithShellCollision(timePlayerInputDeactivatedAfterCollision, transform.forward * hitPower);
            Destroy(gameObject);
        }
    }

    private void DestroySelf()
    {
        Destroy(gameObject);
    }
}
