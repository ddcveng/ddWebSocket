$(document).ready(function () {
    $(".login").on("click", function () {
        //console.log("klik");
        var $name = $("#username");
        var $pass = $("#password");
        var $packet = {
            username: $name.val(),
            password: $pass.val(),
            operation: $(this).attr("name")
        };
        $.ajax({
            type: "POST",
            url: "/api/login",
            data: $packet,
            success: function (response) {
                if (response.Redirect != null) {
                    window.location.replace(response.Redirect);
                } else if (response.HasIcon == false) {
                    document.getElementById("dialog-default").close();
                    document.getElementById("dialog-icons").showModal();
                } else {
                    $("#status").text(response.Data);
                }
            },
            error: function (response) {
                console.log(response);
            }
        });
    });

    $(".closable-dialog").on('click', function (event) {
        var rect = this.getBoundingClientRect();
        var isInDialog = (rect.top <= event.clientY && event.clientY <= rect.top + rect.height
            && rect.left <= event.clientX && event.clientX <= rect.left + rect.width);
        if (!isInDialog) {
            this.close();
        }
    });

    $(".my-icon").on("click", function () {
        $.ajax({
            type: "GET",
            url: "api/seticon?icon=" + this.id,
            success: function (response) {
                window.location.replace(response.Redirect);
            },
            error: function (err) {
                console.error(err);
            }
        })
    });

    $("#Enter").on("click", function () {
        var dialog = document.getElementById("dialog-default");
        dialog.showModal();
    });
});