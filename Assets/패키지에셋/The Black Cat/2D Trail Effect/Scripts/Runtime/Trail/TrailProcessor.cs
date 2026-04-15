using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheBlackCat.TrailEffect2D
{
    public class TrailProcessor : MonoBehaviour
    {
        public TrailInstance TrailInstance => instance;

        private float currentDepth, timeOrDistance;       
        private bool isSpawning;
        private Vector2 prevPos, prevSpawnPos;
        private Transform trans;
        private TrailInstance instance;
        private Func<float, float, float> equation;        
        private Queue<TrailObject> activeTrail = new Queue<TrailObject>();

        private void OnDisable()
        {
            isSpawning = false;
            TrailManager.Instance?.StopTrail(trans.gameObject);
        }

        public void Initialise(TrailInstance instance)
        {            
            this.instance = instance;
            trans = instance.transform;
            currentDepth = trans.position.z + 1;

            timeOrDistance = 0;
            prevPos = prevSpawnPos = trans.position;
            equation = EasingManager.GetEquation(instance.EaseType); 
            isSpawning = true;

            if (instance.SpawnTrailOnStart)
            {
                Spawn(trans.position);
            }
        }

        public int GetActiveTrailCount()
        {
            return activeTrail.Count;
        }

        public void StopSpawning()
        {
            isSpawning = false;

            if (instance.DisableAllTrailOnStop)
            {
                while (activeTrail.Count > 0)
                {
                    DisableTrail();
                }
            }
            TrailManager.Instance.DisableProcessor(this);
        }

        public void DisableTrail()
        {
            TrailObject trail = activeTrail.Dequeue();            
            TrailManager.Instance.DisableTrail(trail);
            trail.gameObject.SetActive(false);
        }

        private void Spawn(Vector2 position)
        {
            TrailObject trail = TrailManager.Instance.GetTrail();

            Vector3 newPos = new Vector3(position.x, position.y, currentDepth);
            trail.transform.SetPositionAndRotation(newPos, trans.rotation);
            trail.Initialise(this, instance, newPos, equation);

            currentDepth -= 0.00001f;
            currentDepth = currentDepth <= trans.position.z ? trans.position.z + 1 : currentDepth;

            activeTrail.Enqueue(trail);
        }

        private void Update()
        {
            if (isSpawning)
            {
                Vector2 currentPos = trans.position;
                if (instance.SpawnCondition == TrailSpawningCondition.TIME)
                {
                    timeOrDistance += Time.deltaTime;
                    while (timeOrDistance >= instance.TimeBetweenSpawn)
                    {
                        if (instance.TimeBetweenSpawn <= 0) break;

                        Vector2 newPos = Vector2.Lerp(prevSpawnPos, currentPos, instance.TimeBetweenSpawn / timeOrDistance);
                        Spawn(newPos);
                        timeOrDistance -= instance.TimeBetweenSpawn;
                        prevSpawnPos = newPos;
                    }
                }
                else if (instance.SpawnCondition == TrailSpawningCondition.DISTANCE_SINCE_PREV)
                {

                    timeOrDistance = Vector2.Distance(prevSpawnPos, currentPos);
                    while (timeOrDistance >= instance.DistanceBetweenSpawn)
                    {
                        if (instance.DistanceBetweenSpawn <= 0) break;

                        Vector2 newPos = prevSpawnPos + (currentPos - prevSpawnPos).normalized * instance.DistanceBetweenSpawn;
                        Spawn(newPos);
                        prevSpawnPos = newPos;
                        timeOrDistance -= instance.DistanceBetweenSpawn;
                    }
                }
                else
                {
                    timeOrDistance += Vector2.Distance(prevPos, currentPos);
                    bool bl = Vector2.Distance(prevPos, currentPos) >= instance.DistanceBetweenSpawn;

                    while (timeOrDistance >= instance.DistanceBetweenSpawn)
                    {
                        if (instance.DistanceBetweenSpawn <= 0) break;

                        if (bl)
                        {
                            Vector2 newPos = prevSpawnPos + (currentPos - prevSpawnPos).normalized * instance.DistanceBetweenSpawn;
                            Spawn(newPos);
                            prevSpawnPos = newPos;
                            timeOrDistance -= instance.DistanceBetweenSpawn;
                        }
                        else
                        {
                            Vector2 newPos = currentPos;
                            newPos = prevSpawnPos + Vector2.ClampMagnitude(newPos - prevSpawnPos, instance.DistanceBetweenSpawn);
                            Spawn(newPos);
                            prevSpawnPos = newPos;
                            timeOrDistance -= instance.DistanceBetweenSpawn;
                        }
                    }
                }

                prevPos = currentPos;
            }
        }
    }
}