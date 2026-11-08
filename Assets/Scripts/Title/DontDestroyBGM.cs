using UnityEngine;

public class DontDestroyBGM : MonoBehaviour
{

    private static DontDestroyBGM instance = null;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }


    void Awake()
    {
        if (instance == null)
        {

            //이 오브젝트를 '씬이 바뀌어도 파괴되지 않는 오브젝트'로 지정.
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            //만약 불러오기에서 다시 타이틀로 돌아왔을 때,
            //중복해서 노래가 겹쳐서 나오는 것을 방지하기 위해 새로 생긴 것을 파괴합니다.
            Destroy(gameObject);
        }
    }


}
