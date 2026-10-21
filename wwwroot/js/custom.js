// to get current year
function getYear() {
    var yearElement = document.getElementById("displayYear");

    if (yearElement) {
        yearElement.innerHTML = new Date().getFullYear();
    }
}

getYear();

 // isotope js
$(window).on('load', function () {
    var $grid = $(".grid").first();

    // Do not initialize Isotope on the Home page.
    // The Home page has its own three-item filter.
    if (document.getElementById("viewMoreLink")) {
        return;
    }

    // Keep Isotope filtering for other pages.
    var $menuGrid = $grid.isotope({
        itemSelector: ".all",
        percentPosition: false,
        masonry: {
            columnWidth: ".all"
        }
    });

    $(".filters_menu li").off("click").on("click", function () {
        $(".filters_menu li").removeClass("active");
        $(this).addClass("active");

        $menuGrid.isotope({
            filter: $(this).attr("data-filter")
        });
    });
});

// nice select
$(document).ready(function() {
    //$('select').niceSelect();
  });

/** google_map js **/
function initMap() {
  var location = { lat: 40.7128, lng: -74.0060 }; // Replace with your coordinates

  var map = new google.maps.Map(document.getElementById("googleMap"), {
    zoom: 15,
    center: location
  });

  var marker = new google.maps.Marker({
    position: location,
    map: map
  });
}

function myMap() {
    var mapProp = {
        center: new google.maps.LatLng(40.712775, -74.005973),
        zoom: 18,
    };
    var map = new google.maps.Map(document.getElementById("googleMap"), mapProp);
}

// client section owl carousel
/*$(".client_owl-carousel").owlCarousel({
    loop: true,
    margin: 0,
    dots: false,
    nav: true,
    navText: [],
    autoplay: true,
    autoplayHoverPause: true,
    navText: [
        '<i class="fa fa-angle-left" aria-hidden="true"></i>',
        '<i class="fa fa-angle-right" aria-hidden="true"></i>'
    ],
    responsive: {
        0: {
            items: 1
        },
        768: {
            items: 2
        },
        1000: {
            items: 2
        }
    }
}); */