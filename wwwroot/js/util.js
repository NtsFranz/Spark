function httpGetAsync(theUrl, callback, failCallback = null) {
    let xmlHttp = new XMLHttpRequest();
    xmlHttp.onreadystatechange = function () {
        if (xmlHttp.readyState === 4) {
            if (xmlHttp.status === 200) {
                callback(xmlHttp.responseText);
            }
            else if (failCallback != null) {
                failCallback();
            }
        }
    }
    xmlHttp.open("GET", theUrl, true); // true for asynchronous 
    xmlHttp.send(null);
}

function toMinutesString(seconds) {
    const mins = seconds / 60;
    let secs = Math.floor(seconds % 60);
    if (secs < 10) {
        secs = "0" + secs;
    }
    return Math.floor(mins) + ":" + secs;
}

function write(className, data) {
    if (data === undefined || data == null || data.toString() === 'undefined') {
        data = "";
    }

    const elements = document.getElementsByClassName(className);
    Array.from(elements).forEach(e => {
        e.innerHTML = data;
        if (data === "") {
            e.style.opacity = "0";
        } else {
            e.style.opacity = 1;
            e.classList.remove('hide');
        }
    });
}

function writeHREF(className, data) {
    if (data === undefined || data.includes('undefined')) return;

    const elements = document.getElementsByClassName(className);
    Array.from(elements).forEach(e => {
        e.href = data;
        e.style.opacity = 1;
        e.classList.remove('hide');
    });
}

function writeValue(className, data) {
    if (data === undefined || data.includes('undefined')) return;

    const elements = document.getElementsByClassName(className);
    Array.from(elements).forEach(e => {
        e.value = data;
        e.style.opacity = 1;
        e.classList.remove('hide');
    });
}

function writeChecked(className, data) {
    if (data === undefined) return;

    const elements = document.getElementsByClassName(className);
    Array.from(elements).forEach(e => {
        e.checked = data;
        e.style.opacity = 1;
        e.classList.remove('hide');
    });
}

function setImage(className, src_) {
    if (src_ === undefined || src_ === "") {
        src_ = "";
    }

    const elements = document.getElementsByClassName(className);
    Array.from(elements).forEach(e => {
        e.src = src_;
        if (src_ === "") {
            e.style.opacity = "0";
        } else {
            e.style.opacity = 1;
            e.classList.remove('hide');
        }
    });
}

function removeAllChildNodes(parent) {
    while (parent.firstChild) {
        parent.removeChild(parent.firstChild);
    }
}