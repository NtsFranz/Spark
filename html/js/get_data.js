var ip = "127.0.0.1"
var port = "6721"

function get_data() {
    var url = "http://"+ip+":"+port+"/session";
    httpGetAsync(url, update_minimap)
}

function update_minimap(data) {
    data = JSON.parse(data);
    console.log(data);
}



function httpGetAsync(theUrl, callback) {
    var xmlHttp = new XMLHttpRequest();
    xmlHttp.onreadystatechange = function () {
        if (xmlHttp.readyState == 4 && xmlHttp.status == 200)
            callback(xmlHttp.responseText);
    }
    xmlHttp.open("GET", theUrl, true); // true for asynchronous 
    xmlHttp.send(null);
}

get_data();