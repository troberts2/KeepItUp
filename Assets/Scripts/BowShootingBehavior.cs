using System.Collections;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public class BowShootingBehavior : MonoBehaviour
{
    [SerializeField] private Transform arrowSpawnPos;
    [SerializeField] private Transform arrowFullyDrawnBackPos;
    [SerializeField] private GameObject arrowPrefab;
    [SerializeField] private float arrowLaunchMaxPower = 5f;
    [SerializeField] private float arrowLaunchMinPower = 2f;
    [SerializeField] private float timeToMaxPower = 1f;
    [SerializeField] private float reloadArrowTime = 1f;
    [SerializeField] private GameObject arrowReloadedIndicator;
    [SerializeField] private LayerMask excludeLayer;
    private bool isCurrentlyShooting = false;
    private PlayerInputActions inputActions;
    private GameObject instantiatedArrow;
    private bool _requestedDrawBack;
    private Camera playerCamera;

    // Start is called before the first frame update
    void Start()
    {
        inputActions = new PlayerInputActions();
        inputActions.Enable();
        playerCamera = Camera.main;
    }

    private void OnDestroy() {
        inputActions.Dispose();
    }

    public void UpdateInput(CharacterInput Input)
    {
        _requestedDrawBack = Input.Shoot;
    }

    // Update is called once per frame
    void Update()
    {
        //if left clickstart the coroutine
        if(_requestedDrawBack && !isCurrentlyShooting)
        {
            StartCoroutine(DrawBackArrow());
        }
    }

    private IEnumerator DrawBackArrow(){
        isCurrentlyShooting = true;
        if(drawBack != null)
        {
            StopCoroutine(drawBack);
        }

        float elapsedTime = 0f;
        while(_requestedDrawBack)
        {
            if(elapsedTime < timeToMaxPower)
            {
                arrowReloadedIndicator.transform.position = Vector3.Lerp(arrowReloadedIndicator.transform.position, arrowFullyDrawnBackPos.position, elapsedTime/timeToMaxPower);
            }
            
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        if(elapsedTime/timeToMaxPower > .1f)
        {
            var power = elapsedTime/timeToMaxPower * arrowLaunchMaxPower;
            power = Mathf.Max(power, arrowLaunchMinPower);
            StartCoroutine(ShootArrow(power));
        }
        else
        {
            //relax arrow
            drawBack = StartCoroutine(ResetDraw());
        }
    }

    private float resetDrawTime = .1f;
    private Coroutine drawBack;
    private IEnumerator ResetDraw()
    {
        isCurrentlyShooting = false;
        float elapsedTime = 0f;
        while(!_requestedDrawBack && elapsedTime < resetDrawTime)
        {
            arrowReloadedIndicator.transform.position = Vector3.Lerp(arrowReloadedIndicator.transform.position, arrowSpawnPos.position, elapsedTime/resetDrawTime);

            elapsedTime += Time.deltaTime;
            yield return null;
        }
    }

    private RaycastHit hit;
    private IEnumerator ShootArrow(float power)
    {
        arrowReloadedIndicator.SetActive(false);

        instantiatedArrow = Instantiate(arrowPrefab, arrowSpawnPos.position, transform.rotation);
        Physics.IgnoreCollision(instantiatedArrow.GetComponent<Collider>(), transform.root.GetComponentInChildren<Collider>());
        if(Physics.Raycast(playerCamera.ScreenPointToRay(Input.mousePosition), out hit, 100f, ~excludeLayer))
        {
            var dir = (hit.point - instantiatedArrow.transform.position).normalized;
            instantiatedArrow.GetComponent<Rigidbody>().AddForce(dir * power, ForceMode.Impulse); 
        }else{
           instantiatedArrow.GetComponent<Rigidbody>().AddForce(transform.forward * power, ForceMode.Impulse); 
        }
        

        yield return new WaitForSeconds(reloadArrowTime);

        arrowReloadedIndicator.SetActive(true);
        arrowReloadedIndicator.transform.position = arrowSpawnPos.position;
        isCurrentlyShooting = false;

    }
}
