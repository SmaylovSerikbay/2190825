using UnityEngine;
using Firebase;
using Firebase.Database;
using Firebase.Extensions;
using System.Collections.Generic;

public class FirebaseInit : MonoBehaviour
{
    DatabaseReference databaseReference;

    void Start()
    {
        // ������������� Firebase
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task => {
            if (task.IsCompleted)
            {
                // ���������� URL ��� ���� ������ Firebase ��� ������ �������� \r\n
                FirebaseDatabase database = FirebaseDatabase.GetInstance("https://comeback-2a6b2-default-rtdb.firebaseio.com/");

                // ���������� ���������� ������ �� ���� ������ ����� ���������� ���������
                databaseReference = database.RootReference;

                // ������� ����� WriteDataToFirebase() ��� ���������� ������ ������
                // WriteDataToFirebase();  // ��� ������ ����� ������� ��� ����������������
            }
            else
            {
                Debug.LogError("�� ������� ���������������� Firebase.");
            }
        });
    }

    // ������ ������� ��� ������ ������ � Firebase
    
}
