# FaceGood
LiveLink Source for receiving JSON over sockets.


## UDP Protocol
JSON format
```
{
"FACEGOOD":{  #subjectName
    "FrameId" : 1, #frame id ,option
    "Properties":[
        {"Name":"A","Value":0.0},  # item name and it's value
        {"Name":"B","Value":0.0}
        ],
    "Joints":{
    "Names":["JA","JB"],
    "ParentIdx":[0,0],
    "Transforms":[[0,0,0,0,0,0],[0,0,0,0,0,0]]
    }
    }
}
```
