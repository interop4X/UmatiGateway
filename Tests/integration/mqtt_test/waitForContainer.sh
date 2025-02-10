#!/bin/bash
NEXT_WAITTIME=0
WAITTIME_LIMIT_SEC=600
while [[ "$(docker logs  mqtt_test-gateway-1 | grep -c "Publish Bad List Maschine Nodes finish.")" > "2"  && "$NEXT_WAITTIME" != "$WAITTIME_LIMIT_SEC" ]]
do 
    echo "Waiting for test container to become ready since ${NEXT_WAITTIME}s..."
    sleep 5
    NEXT_WAITTIME=$((NEXT_WAITTIME + 5))
done
sleep 5