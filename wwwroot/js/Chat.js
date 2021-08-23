$(document).ready(function () {
    var messageInput = $("#message");
    var messageBox = document.getElementById("messageList");

    repopulateSelect();

    $("#sendMessage").on("click", function () {
        var $message = {
            message: messageInput.val(),
            icon: document.getElementById("userIcon").innerHTML
        }
        $.ajax({
            type: "POST",
            url: "/api/messages",
            data: $message,
            success: function (messages) {
                messageBox.innerHTML = "";
                messages.forEach(addMessage, messageBox);
            }
        });
    });

    $(".closable-dialog").on('click', function (event) {
        var X = event.clientX;
        var Y = event.clientY;
        if (X != 0 && Y != 0){
            var rect = this.getBoundingClientRect();
            var isInDialog = (rect.top <= Y && Y <= rect.top + rect.height
                && rect.left <= X && X <= rect.left + rect.width);
            if (!isInDialog) {
                this.close();
            }
        }
    });

    $("#chatroomName").on("click", function () {
        copyToClipboard(document.getElementById("copyToClipboard").innerHTML);
    });

    $("#enterRoom").on("click", function () {
        var room = {
            ID: $("#default_select").val(),
            Name: $("#default_select option:selected").text()
        };
        console.log(room);
        changeRoom(room);
    });

    $("#joinRoom").on("click", function () {
        var roomId = document.getElementById("roomToJoin");
        $.ajax({
            type: "GET",
            url: "/api/join?id=" + roomId.value,
            success: function () {
                repopulateSelect();
            },
            error: function (e) {
                console.log(e);
            }
        });
    });

    $("#userName").on("click", function () {
        $.ajax({
            type: "GET",
            url: "/api/logout",
            success: function () {
                window.location.replace("/");
            }
        });
    });

    setInterval(function () { getMessages();}, 10*1000);
});

function copyToClipboard(text) {
    var input = document.body.appendChild(document.createElement("input"));
    input.value = text;
    input.focus();
    input.select();
    document.execCommand('copy');
    input.parentNode.removeChild(input);
}

function addNameToList(obj) {
    var option = document.createElement("option");
    option.value = obj.ID;
    option.innerHTML = obj.Name;
    this.appendChild(option);
}

function addMessage(message) {
    var activeUser = document.getElementById("userName").textContent;
    var side = "-left"
    if (activeUser == message.Sender){
        side = "-right";
    }
    var text = document.createElement("p");
    text.innerHTML = decodeURIComponent(message.Body.replace(/\++/g, " "));

    var baloon = document.createElement("div");
    baloon.classList.add("nes-balloon", "from"+side);
    baloon.appendChild(text);

    var icon = document.createElement("i");
    icon.classList.add(message.Icon);

    var sender = document.createElement("p");
    sender.innerHTML = message.Sender;
    sender.classList.add("red");

    var section = document.createElement("section");
    section.classList.add("message", side);
    section.appendChild(sender);

    if (side == "-left") {
        section.appendChild(icon);
        section.appendChild(baloon);
    }
    else{
        section.appendChild(baloon);
        section.appendChild(icon);
        section.classList.add("fill-parent");
    }
    var div = document.createElement("div");
    div.classList.add("full-width");
    div.appendChild(section);

    this.appendChild(div);
    // var li = document.createElement("li");
    // li.textContent = message.Sender + ": " + message.Body.replace(/\++/g, " ");
    // this.appendChild(li);
}

function getMessages() {
    var container = document.getElementById("messageList");
    var roomId = document.getElementById("copyToClipboard");
    $.ajax({
        type: "GET",
        url: "/api/messages?id=" + roomId.innerHTML,
        success: function (messages) {
            console.log(messages);
            container.innerHTML = "";
            messages.forEach(addMessage, container);
        },
        error: function (_) {
            console.log("cant fetch messages");
        }
    });
}

function repopulateSelect() {
    var rooms = document.getElementById("default_select");
    var roomId = document.getElementById("copyToClipboard");
    $.ajax({
        type: "GET",
        url: "/api/chatinit",
        success: function (chatRoomList) {
            chatRoomList.forEach(addNameToList, rooms);
            setLoggedInUser();
            console.log(roomId.innerHTML);
            if (roomId.innerHTML == "" && chatRoomList.length != 0)
            {
                console.log(chatRoomList[0]);
                changeRoom(chatRoomList[0]);
            }
        },
        error: function (_) {
            console.log("bad juju");
        }
    });
}

function changeRoom(room) {
    var roomId = document.getElementById("copyToClipboard");
    if (roomId != room.ID)
    {
        var roomName = document.getElementById("chatroomName");
        roomName.innerHTML =  room.Name;
        roomId.innerHTML   =  room.ID;
        document.getElementById("messageList").innerHTML = "";
    }
    getMessages();
};

function setLoggedInUser() {
    var userName = document.getElementById("userName");
    var userIcon = document.getElementById("userIcon");
    $.ajax({
        type: "GET",
        url: "/api/getuser",
        success: function (userData) {
            userName.innerHTML = userData.Username;
            userIcon.innerHTML = userData.Icon;
        },
        error: function (e) {
            console.log(e);
        }
    });
}
